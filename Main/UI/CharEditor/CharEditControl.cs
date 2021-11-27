using System;
using System.Drawing;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;


namespace FoenixToolkit.UI
{
    public class CharEditControl : Box
    {
        int _charIndex = 0;
        readonly int _columns = 8;
        int _rowsPerChar = 8;

        byte[] _reloadData = null;
        byte[] _clipData = null;
        byte[] _fontData = new byte[8 * 256];
        byte[] _characterData = new byte[16];
        bool[,] _grid = new bool[8, 8];

        Gdk.ModifierType _mouseButtonHeld = Gdk.ModifierType.None;
        bool _colorHeld = false;

        public event EventHandler CharacterSaved;

        readonly Cairo.Color lightGreen = new(Color.LightGreen.R / 255.0, Color.LightGreen.G / 255.0, Color.LightGreen.B / 255.0);
        readonly Cairo.Color darkGray = new(Color.DarkGray.R / 255.0, Color.DarkGray.G / 255.0, Color.DarkGray.B / 255.0);

#pragma warning disable CS0649  // never assigned
        //[GUI] Button btnDown;
        //[GUI] Button btnUp;
        //[GUI] Button btnLeft;
        //[GUI] Button btnRight;
        //[GUI] Button btnClear;
        //[GUI] Button btnReload;
        //[GUI] Button btnCopy;
        //[GUI] Button btnPaste;
        //[GUI] Button btnSave;
        [GUI] DrawingArea daCanvas;
#pragma warning restore CS0649

        public CharEditControl() : this(new Builder("CharEditControl.ui")) { }

        private CharEditControl(Builder builder) : base(builder.GetRawOwnedObject("CharEditControl"))
        {
            builder.Autoconnect(this);
        }

        internal void LoadCharacter(byte[] FontData, int selectedIndex, int bytesPerCharacter)
        {
            _fontData = FontData;
            _charIndex = selectedIndex;
            _rowsPerChar = bytesPerCharacter;

            LoadCharacter();
        }

        private void LoadCharacter()
        {
            _characterData = new byte[_rowsPerChar];

            int pos = _charIndex * _rowsPerChar;
            if (pos < 0 || pos >= _fontData.Length)
                return;

            for (int i = 0; i < _rowsPerChar; ++i)
                _characterData[i] = _fontData[pos + i];

            _reloadData = new byte[_rowsPerChar];
            _characterData.CopyTo(_reloadData, 0);

            LoadPixels();

            daCanvas.QueueDraw();
        }

        private void LoadPixels()
        {
            for (int y = 0; y < _rowsPerChar; ++y)
            {
                int row = 0;
                for (int x = 0; x < _columns; ++x)
                {
                    int bit = (int)Math.Pow(2, (_columns - x - 1));
                    _grid[x, y] = (_characterData[y] & bit) == bit;
                }

                _characterData[y] = (byte)row;
            }
        }

        private Point GetPixel(Point location)
        {
            return new Point(
                location.X / (daCanvas.AllocatedWidth / _columns),
                location.Y / (daCanvas.AllocatedHeight / _rowsPerChar)
            );
        }

        private void SaveCharacter()
        {
            SavePixels();

            int j = _charIndex * _rowsPerChar;
            for (int i = 0; i < _rowsPerChar; ++i)
                _fontData[j + i] = _characterData[i];

            CharacterSaved?.Invoke(this, new EventArgs());
        }

        private void SavePixels()
        {
            for (int y = 0; y < _rowsPerChar; ++y)
            {
                int row = 0;

                for (int x = 0; x < _columns; ++x)
                {
                    int bit = (int)Math.Pow(2, (_columns - x - 1));
                    row |= (_grid[x, y] ? bit : 0);
                }

                _characterData[y] = (byte)row;
            }
        }

        private void Redraw()
        {
            daCanvas.QueueDraw();

            SaveCharacter();
        }

        private void on_btnUp_clicked(object sender, EventArgs e)
        {
            // copy the rows up one line
            for (int y = 0; y < _rowsPerChar - 1; ++y)
                for (int x = 0; x < _columns; ++x)
                    _grid[x, y] = _grid[x, y + 1];

            // blank the bottom row
            for (int x = 0; x < _columns; ++x)
                _grid[x, _rowsPerChar - 1] = false;

            Redraw();
        }

        private void on_btnDown_clicked(object sender, EventArgs e)
        {
            // copy the rows down one line
            for (int y = _rowsPerChar - 1; y > 0; --y)
                for (int x = 0; x < _columns; ++x)
                    _grid[x, y] = _grid[x, y - 1];

            // blank the top row
            for (int x = 0; x < _columns; ++x)
                _grid[x, 0] = false;

            Redraw();
        }

        private void on_btnLeft_clicked(object sender, EventArgs e)
        {
            // copy the columns left one position
            for (int y = 0; y < _rowsPerChar; ++y)
                for (int x = 0; x < _columns - 1; ++x)
                    _grid[x, y] = _grid[x + 1, y];

            // blank the last column
            for (int y = 0; y < _rowsPerChar; ++y)
                _grid[_columns - 1, y] = false;

            Redraw();
        }

        private void on_btnRight_clicked(object sender, EventArgs e)
        {
            // copy the columns right one position
            for (int y = 0; y < _rowsPerChar; ++y)
                for (int x = _columns - 1; x > 0; --x)
                    _grid[x, y] = _grid[x - 1, y];

            // blank the first column
            for (int y = 0; y < _rowsPerChar; ++y)
                _grid[0, y] = false;

            Redraw();
        }

        private void on_btnClear_clicked(object sender, EventArgs e)
        {
            for (int y = 0; y < _rowsPerChar; ++y)
                for (int x = 0; x < _columns; ++x)
                    _grid[x, y] = false;

            Redraw();
        }

        private void on_btnReload_clicked(object sender, EventArgs e)
        {
            if (_reloadData == null)
                return;

            _reloadData.CopyTo(_characterData, 0);
            LoadPixels();

            Redraw();
        }

        private void on_btnCopy_clicked(object sender, EventArgs e)
        {
            SavePixels();

            _clipData = new byte[_rowsPerChar];
            _characterData.CopyTo(_clipData, 0);
        }

        private void on_btnPaste_clicked(object sender, EventArgs e)
        {
            if (_clipData == null)
                return;

            _clipData.CopyTo(_characterData, 0);
            LoadPixels();

            Redraw();
        }

        private void on_btnSave_clicked(object sender, EventArgs e)
        {
            SaveCharacter();
        }

        private void on_daCanvas_button_press_event(object sender, ButtonPressEventArgs e)
        {
            if (e.Event.Type == Gdk.EventType.ButtonPress && e.Event.State == Gdk.ModifierType.Button1Mask)
            {
                _mouseButtonHeld = e.Event.State;

                Point p = GetPixel(new Point((int)e.Event.X, (int)e.Event.Y));
                if (p.X < 0 || p.X >= _columns || p.Y < 0 || p.Y >= _rowsPerChar)
                    return;
    
                _colorHeld = !_grid[p.X, p.Y];
                _grid[p.X, p.Y] = _colorHeld;

                daCanvas.QueueDraw();
            }
        }

        private void on_daCanvas_button_release_event(object sender, ButtonReleaseEventArgs e)
        {
            if (e.Event.Type == Gdk.EventType.ButtonRelease && e.Event.State == Gdk.ModifierType.Button1Mask)
                _mouseButtonHeld = Gdk.ModifierType.None;

            Redraw();
        }

        private void on_daCanvas_motion_notify_event(object sender, MotionNotifyEventArgs e)
        {
            if (_mouseButtonHeld == Gdk.ModifierType.Button1Mask)
            {
                Point p = GetPixel(new Point((int)e.Event.X, (int)e.Event.Y));
                if (p.X < 0 || p.X >= _columns || p.Y < 0 || p.Y >= _rowsPerChar)
                    return;

                bool i = _grid[p.X, p.Y];
                if (i != _colorHeld)
                {
                    _grid[p.X, p.Y] = _colorHeld;

                    daCanvas.QueueDraw();
                }
            }
        }

        private void on_daCanvas_draw(object sender, DrawnArgs e)
        {
            var cr = e.Cr;

            Gdk.Rectangle rec;
            int bl;
            daCanvas.GetAllocatedSize(out rec, out bl);

            double pixelWidth = rec.Width / _columns;
            double pixelHeight = rec.Height / _rowsPerChar;

            for (int y = 0; y < _rowsPerChar; ++y)
            {
                for (int x = 0; x < _columns; ++x)
                {
                    Cairo.Rectangle pixel = new(
                        x * pixelWidth, y * pixelHeight,
                        pixelWidth, pixelHeight);

                    if (_grid[x, y])
                        cr.SetSourceColor(lightGreen);
                        cr.Rectangle(pixel);
                        cr.Fill();

                    cr.SetSourceColor(darkGray);
                    cr.Rectangle(pixel);
                    cr.Stroke();
                }
            }
        }
    }
}
