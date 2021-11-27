using System;
using System.Drawing;
using System.IO;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;


namespace FoenixToolkit.UI
{
    public class CharViewerControl : Box
    {
        const int CHARSET_SIZE = 2048;

        public int BitsPerRow = 8;
        public int BytesPerCharacter = 8;
        readonly int Columns = 16;
        readonly int Rows = 16;
        int Col1X = 28;
        int Row1Y = 28;
        int CharacterWidth = 12;
        int CharacterHeight = 12;
        int BASELINE_OFFSET = 10;
        int HoveredChar = -1;

        public int SelectedIndex;
        public int SelectionLength = 1;
        public bool ShowSelected = false;

        public delegate void CharacterSelectedEvent(object sender, EventArgs e);
        public event CharacterSelectedEvent CharacterSelected;

        public byte[] FontData = new byte[8 * 256];

        public Gdk.ModifierType MouseButton { get; private set; }

        private Cairo.Color lightBlue = new(Color.LightBlue.R / 255.0, Color.LightBlue.G / 255.0, Color.LightBlue.B / 255.0);
        private Cairo.Color royalBlue = new(Color.RoyalBlue.R / 255.0, Color.RoyalBlue.G / 255.0, Color.RoyalBlue.B / 255.0);

        private Cairo.Color lightGray = new(Color.LightGray.R / 255.0, Color.LightGray.G / 255.0, Color.LightGray.B / 255.0);
        private Cairo.Color gray = new(Color.Gray.R / 255.0, Color.Gray.G / 255.0, Color.Gray.B / 255.0);

#pragma warning disable CS0649  // never assigned
        // [GUI] Box CharViewer;
#pragma warning restore CS0649

        public CharViewerControl() : this(new Builder("CharViewerControl.ui")) { }

        private CharViewerControl(Builder builder) : base(builder.GetRawOwnedObject("CharViewerControl"))
        {
            builder.Autoconnect(this);
        }

        public byte[] LoadBin(string Filename)
        {
            if (!System.IO.File.Exists(Filename))
                return new byte[CHARSET_SIZE];

            BinaryReader br = new(new FileStream(Filename, FileMode.Open));
            byte[] data = br.ReadBytes(CHARSET_SIZE);

            return data;
        }

        public void SaveBin(string Filename, byte[] data)
        {
            System.IO.File.WriteAllBytes(Filename, data);
        }

        public byte[] LoadPNG(string Filename)
        {
            if (!System.IO.File.Exists(Filename))
                return new byte[CHARSET_SIZE];

            byte[] data = new byte[CHARSET_SIZE];

            Bitmap img = System.Drawing.Image.FromFile(Filename) as Bitmap;
            if (img == null)
                throw new Exception("Not a bitmap file");

            int pos = 0;
            for (int y = 0; y < img.Height; y += BytesPerCharacter)
            {
                for (int x = 0; x < img.Width; x += 8)
                {
                    for (int cy = y; cy < y + BytesPerCharacter; ++cy)
                    {
                        int row = 0;
                        int bit = 128;

                        for (int cx = x; cx < x + 8; ++cx)
                        {
                            var pixel = img.GetPixel(cx, cy);
                            if (pixel.R > 0)
                                row |= bit;

                            bit = (byte)(bit >> 1);
                        }

                        data[pos++] = (byte)row;
                    }
                }
            }

            return data;
        }

        // Copy all of the characters from the input to the output 
        // useful for converting BIN to PNG or PNG to BIN
        public void CopyAll()
        {
            CopyBlock(FontData, 0, 0, 256);

            QueueDraw();
        }

        public void CopyNonPET()
        {
            // control characters (0-31)
            CopyBlock(FontData, 0x0, 0x0, 32);

            // grave (`)
            CopyBlock(FontData, 0x140, 0x60, 1);

            // {|}~ and 127
            CopyBlock(FontData, 0x15b, 0x7b, 5);

            //solid block
            CopyBlock(FontData, 0xe0, 0xa0, 1);

            // new custom glyphs (last two rows)
            CopyBlock(FontData, 0xe0, 0xe0, 32);

            QueueDraw();
        }

        private void DrawCharSet(byte[] data, Graphics g, int StartX, int StartY)
        {

        }

        internal void Clear()
        {
            FontData = new byte[CHARSET_SIZE];

            QueueDraw();
        }

        /// <summary>
        /// Re-orders the loaded character set, placing the characters in ASCII order. 
        /// <para>Upper case letters start at 64</para>
        /// <para>Lower case letters start at 97</para>
        /// <para>Shifted symbols start at 192 (letter + 128)
        /// <para>C= PET symbols start at 160</para>
        /// <para>New symbols start at 224</para>
        /// <para>0-31 and 128-159 are control characters and not used</para>
        /// </summary>
        public void ConvertPETtoASCII()
        {
            // 32 (space) to 63 (?)
            CopyBlock(FontData, 32, 32, 32);

            // upper case letters
            CopyBlock(FontData, 0, 64, 32);

            // lower case letters
            CopyBlock(FontData, 0x101, 0x61, 26);

            //solid block
            CopyBlock(FontData, 0xe0, 0xa0, 1);

            // C= PET symbols
            CopyBlock(FontData, 0x61, 0xa1, 31);

            // Shifted PET symbols
            CopyBlock(FontData, 0x40, 0xc0, 32);

            QueueDraw();
        }

        private void CopyBlock(byte[] source, int sourceIndex, int destIndex, int count)
        {
            for (int i = 0; i < count; ++i)
                CopyCharacter(sourceIndex + i, destIndex + i);
        }

        private void CopyCharacter(int sourceIndex, int destIndex)
        {
            int sp = sourceIndex * BytesPerCharacter;
            int dp = destIndex * BytesPerCharacter;

            for (int i = 0; i < BytesPerCharacter; ++i)
                FontData[dp + i] = FontData[sp + i];
        }

        protected void OnCharacterSelected()
        {
            if (CharacterSelected == null)
                return;

            EventArgs e = new();
            CharacterSelected(this, e);
        }

        private void on_CharViewerControl_button_press_event(object sender, ButtonPressEventArgs e)
        {
            if (e.Event.Type == Gdk.EventType.ButtonPress && e.Event.State == Gdk.ModifierType.Button1Mask)
            {
                MouseButton = Gdk.ModifierType.Button1Mask;

                SelectedIndex = HoveredChar;
                SelectionLength = 1;

                QueueDraw();
            }
        }

        private void on_CharViewerControl_button_release_event(object sender, ButtonReleaseEventArgs e)
        {
            if (e.Event.Type == Gdk.EventType.ButtonRelease && e.Event.State == Gdk.ModifierType.Button1Mask)
            {
                SelectionLength = HoveredChar - SelectedIndex + 1;

                QueueDraw();

                OnCharacterSelected();
            }

            MouseButton = Gdk.ModifierType.None;
        }

        private void on_CharViewerControl_motion_notify_event(object sender, MotionNotifyEventArgs e)
        {
            Point p = new()
            {
                X = (int)((e.Event.X - Col1X) / CharacterWidth),
                Y = (int)((e.Event.Y - Row1Y) / CharacterHeight)
            };

            if (p.X < 0 || p.X >= Columns)
                HoveredChar = -1;
            else if (p.Y < 0 || p.Y >= Rows)
                HoveredChar = -1;
            else
                HoveredChar = p.Y * Columns + p.X;
        }

        private void on_CharViewerControl_draw(object sender, DrawnArgs e)
        {
            var cr = e.Cr;

            cr.SetSourceColor(royalBlue);
            cr.Rectangle(0, 0, AllocatedWidth, AllocatedHeight);
            cr.Fill();

            cr.SelectFontFace("Consolas", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            cr.SetFontSize(9);

            int StartX = 0;
            int StartY = 0;

            if (FontData == null)
                return;

            int characters = FontData.Length / BytesPerCharacter;
            int bitWidth = 2;
            int bitHeight = 2;

            CharacterWidth = bitWidth * 8 + 4;
            CharacterHeight = bitHeight * BytesPerCharacter + 4;

            Col1X = StartX + 28;
            int lastCol = Col1X + CharacterWidth * Columns;
            Row1Y = StartY + 28;
            int lastRow = Row1Y + CharacterHeight * Rows;

            if (ShowSelected)
            {
                cr.SetSourceColor(lightGray); 
                cr.MoveTo(0, 0 + BASELINE_OFFSET);
                cr.ShowText(SelectedIndex.ToString());
            }

            int x = Col1X;
            int y = StartY;

            for (int i = 0; i < Columns; ++i)
            {
                cr.SetSourceColor(gray); 
                cr.MoveTo(x - 2, Row1Y - 4);
                cr.LineTo(x - 2, lastRow);
                cr.Stroke();

                cr.SetSourceColor(lightGray); 
                cr.MoveTo(x, y + 6 + BASELINE_OFFSET);
                cr.ShowText(" " + i.ToString("X"));

                x += CharacterWidth;
            }

            cr.SetSourceColor(gray); 
            cr.MoveTo(x - 2, Row1Y - 4);
            cr.LineTo(x - 2, lastRow);
            cr.Stroke();

            x = StartX;
            y = Row1Y;

            for (int i = 0; i < Rows; ++i)
            {
                cr.SetSourceColor(gray); 
                cr.MoveTo(Col1X - 4, y - 2);
                cr.LineTo(lastCol, y - 2);
                cr.Stroke();

                cr.SetSourceColor(lightGray); 
                cr.MoveTo(x + 6, y + BASELINE_OFFSET);
                cr.ShowText(i.ToString("X") + "0");

                y += CharacterHeight;
            }

            cr.SetSourceColor(gray); 
            cr.MoveTo(Col1X - 4, y - 2);
            cr.LineTo(lastCol, y - 2);
            cr.Stroke();

            x = Col1X;
            y = Row1Y;
            for (int i = 0; i < characters; ++i)
            {
                int x0 = x;
                if (i >= SelectedIndex && i < SelectedIndex + SelectionLength)
                cr.SetSourceColor(gray);
                cr.Rectangle(x - 2, y - 2, CharacterWidth, CharacterHeight);
                cr.Stroke();

                for (int charRow = 0; charRow < BytesPerCharacter; charRow++)
                {
                    int pos = i * BytesPerCharacter + charRow;
                    if (pos < 0 || pos >= FontData.Length)
                        return;

                    byte b = FontData[pos];
                    for (int bit = 128; bit > 0;)
                    {
                        if ((b & bit) > 0)
                        {
                            cr.SetSourceColor(lightGray);
                            cr.Rectangle(x, y, bitWidth, bitHeight);
                            cr.Stroke();
                        }

                        x += bitWidth;
                        bit >>= 1;
                    }

                    x = x0;
                    y += bitHeight;
                }

                x = Col1X + ((i + 1) % Columns * CharacterWidth);
                y = Row1Y + ((int)(i + 1) / Rows) * CharacterHeight;
            }
        }
    }
}
