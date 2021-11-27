using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore.MemoryLocations;


namespace FoenixToolkit.UI
{
    class TileEditorWindow : Window
    {
        private int selectedX = -1;
        private int selectedY = -1;
        private int hoverX = -1;
        private int hoverY = -1;
        private const int TILE_WIDTH = 17;
        private int selectedTilemap = 0;
        const int BASELINE_OFFSET = 12;

        private MemoryManager MemMgr;

        private Cairo.Color red = new(Color.Red.R / 255.0, Color.Red.G / 255.0, Color.Red.B / 255.0);
        private Cairo.Color white = new(Color.White.R / 255.0, Color.White.G / 255.0, Color.White.B / 255.0);
        private Cairo.Color yellow = new(Color.Yellow.R / 255.0, Color.Yellow.G / 255.0, Color.Yellow.B / 255.0);

#pragma warning disable CS0649  // never assigned
        [GUI] CheckButton chkStride256;
        [GUI] CheckButton chkTilemapEnabled;
        [GUI] ComboBoxText cboLUT;
        [GUI] ComboBoxText cboTileset;
        [GUI] DrawingArea daCanvas;
        [GUI] Entry txtTilemapAddress;
        [GUI] Entry txtTilesetAddress;
        [GUI] Entry txtHeight;
        [GUI] Entry txtWidth;
        [GUI] Entry txtWindowX;
        [GUI] Entry txtWindowY;
        [GUI] Label lblTilesetSelected;
        [GUI] ToggleButton btnTilemap0;
        [GUI] ToggleButton btnTilemap1;
        [GUI] ToggleButton btnTilemap2;
        [GUI] ToggleButton btnTilemap3;
#pragma warning restore CS0649

        public TileEditorWindow() : this(new Builder("TileEditorWindow.ui")) { }

        private TileEditorWindow(Builder builder) : base(builder.GetRawOwnedObject("TileEditorWindow"))
        {
            builder.Autoconnect(this);
        }

        public void SetMemory(MemoryManager mm)
        {
            MemMgr = mm;
        }

        private int[] LoadLUT(MemoryRAM VKY)
        {
            // Read the color lookup tables
            int lutAddress = MemoryMap.GRP_LUT_BASE_ADDR - MemoryMap.VICKY_BASE_ADDR;
            int lookupTables = 4;
            int[] result = new int[lookupTables * 256];

            for (int c = 0; c < lookupTables * 256; c++)
            {
                byte blue = VKY.ReadByte(lutAddress++);
                byte green = VKY.ReadByte(lutAddress++);
                byte red = VKY.ReadByte(lutAddress++);
                lutAddress++; // skip the alpha channel

                result[c] = (int)(0xFF000000 + (red << 16) + (green << 8) + blue);
            }

            return result;
        }

        private void on_TileEditorWindow_realize(object sender, EventArgs e)
        {
            btnTilemap0.Activate();

            cboTileset.ActiveId = "0";
        }

        private void on_TileEditorWindow_key_press_event(object sender, KeyPressEventArgs e)
        {
            if (e.Event.Key == Gdk.Key.Escape)
                Close();
        }

        /**
         * When the user moves the mouse, highlight the border in yellow and print the number.
         */
        private void on_evtCanvas_motion_notify_event(object sender, MotionNotifyEventArgs e)
        {
            hoverX = (int)(e.Event.X / TILE_WIDTH);
            hoverY = (int)(e.Event.Y / TILE_WIDTH);

            daCanvas.QueueDraw();
        }

        private void on_evtCanvas_button_press_event(object sender, ButtonPressEventArgs e)
        {
            selectedX = (int)(e.Event.X / TILE_WIDTH);
            selectedY = (int)(e.Event.Y / TILE_WIDTH);

            lblTilesetSelected.Text = $"Tile Selected: ${(selectedY * 16 + selectedX).ToString("X2")}";

            daCanvas.QueueDraw();
        }

        /**
         * Draw the tileset with clear lines separating the images 16x16.
         */
        private void on_daCanvas_draw(object sender, DrawnArgs e)
        {
            var cr = e.Cr;

            cr.SelectFontFace("Consolas", Cairo.FontSlant.Normal, Cairo.FontWeight.Normal);
            cr.SetFontSize(14);

            // Read the memory and display the tiles
            Rectangle rect = new(0, 0, 16 * 17 + 1, 16 * 17 + 1);
            Bitmap frameBuffer = new(16 * 17 + 1, 16 * 17 + 1, PixelFormat.Format32bppArgb);
            BitmapData bitmapData = frameBuffer.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            IntPtr p = bitmapData.Scan0;
            int stride = bitmapData.Stride;

            int[] graphicsLUT = LoadLUT(MemMgr.VICKY);
            int lut = Convert.ToInt32(cboLUT.ActiveId);
            int tilesetAddress = Convert.ToInt32(txtTilesetAddress.Text, 16) - 0xB0_0000;

            for (int y = 0; y < 256; ++y)
            {
                for (int x = 0; x < 256; ++x)
                {
                    byte pixel = MemMgr.VIDEO.ReadByte(tilesetAddress + y * 256 + x);
                    if (pixel != 0)
                    {
                        int color = graphicsLUT[lut * 256 + pixel];
                        int destX = x / 16 * TILE_WIDTH + x % 16 + 1;
                        int destY = y / 16 * TILE_WIDTH + y % 16 + 1;
                    }
                }
            }

            //-- frameBuffer.UnlockBits(bitmapData);
            // e.Graphics.DrawImageUnscaled(frameBuffer, rect);
            // frameBuffer.Dispose();

            if (hoverX > -1 && hoverY > -1)
            {
                cr.SetSourceColor(white);
                cr.Rectangle(hoverX * TILE_WIDTH - 1, hoverY * TILE_WIDTH - 1, TILE_WIDTH + 2, TILE_WIDTH + 2);
                cr.Stroke();

                cr.SetSourceColor(yellow);
                cr.Rectangle(hoverX * TILE_WIDTH, hoverY * TILE_WIDTH, TILE_WIDTH, TILE_WIDTH);
                cr.Stroke();

                cr.SetSourceColor(white);
                cr.MoveTo(hoverX * TILE_WIDTH, hoverY * TILE_WIDTH + 2 + BASELINE_OFFSET);
                cr.ShowText((hoverY * 16 + hoverX).ToString("X2"));
            }

            if (selectedX > -1 && selectedY > -1)
            {
                cr.SetSourceColor(red);
                cr.Rectangle(selectedX * TILE_WIDTH - 1, selectedY * TILE_WIDTH - 1, TILE_WIDTH + 2, TILE_WIDTH + 2);
                cr.Stroke();

                cr.Rectangle(selectedX * TILE_WIDTH, selectedY * TILE_WIDTH, TILE_WIDTH, TILE_WIDTH);
                cr.Stroke();
            }
        }

        public void on_btnTilemap_clicked(object sender, EventArgs e)
        {
            ToggleButton selected = sender as ToggleButton;

            // disable the previous button
            btnTilemap0.Active = false;
            btnTilemap1.Active = false;
            btnTilemap2.Active = false;
            btnTilemap3.Active = false;

            selectedTilemap = 0;

            if (selected != null)
            {
                selected.Active = true;

                selectedTilemap = (selected.Name) switch
                {
                    "Tilemap0" => 0,
                    "Tilemap1" => 1,
                    "Tilemap2" => 2,
                    "Tilemap3" => 3,
                    _ => 0
                };
            }

            int addrOffset = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;

            // show if the tilemap is enabled - ignore the LUT, it's not used
            int ControlReg = MemMgr.ReadByte(addrOffset);
            chkTilemapEnabled.Active = (ControlReg & 1) != 0;

            // address in memory
            int tilemapAddr = MemMgr.ReadLong(addrOffset + 1);
            txtTilemapAddress.Text = (tilemapAddr + 0xB0_0000).ToString("X6");

            int width = MemMgr.ReadWord(addrOffset + 4);
            int height = MemMgr.ReadWord(addrOffset + 6);
            txtWidth.Text = width.ToString();
            txtHeight.Text = height.ToString();

            int winX = MemMgr.ReadWord(addrOffset + 8);
            int winY = MemMgr.ReadWord(addrOffset + 10);
            txtWindowX.Text = winX.ToString();
            txtWindowY.Text = winY.ToString();
        }

        /**
         * When a tile is clicked in the GPU window, write the selected tile in memory.
         */
        public void TileClicked_Click(Point tile)
        {
            int tilemapAddress = Convert.ToInt32(txtTilemapAddress.Text, 16);
            int offset = (tile.Y * Convert.ToInt32(txtWidth.Text) + tile.X + 1) * 2;

            if (selectedX != -1 && selectedY != -1)
            {
                // Write the tile value
                byte value = (byte)(selectedY * 16 + selectedX);
                MemMgr.WriteByte(tilemapAddress + offset, value);
                // Write the tileset and LUT - this way we can mix tiles from multiple tilesets in a single map
                int lut = Convert.ToInt32(cboLUT.ActiveId);
                int tset = Convert.ToInt32(cboTileset.ActiveId);
                MemMgr.WriteByte(tilemapAddress + offset + 1, (byte)((lut << 3) + tset));
            }
        }

        private void chkTilemapEnabled_Click(object sender, EventArgs e)
        {
            int addrOffset = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;
            byte ControlReg = MemMgr.ReadByte(addrOffset);
            ControlReg = (byte)((ControlReg & 0xF0) + (chkTilemapEnabled.Active ? 1 : 0));
            MemMgr.WriteByte(addrOffset, ControlReg);
        }

        private void on_btnClearTilemap_clicked(object sender, EventArgs e)
        {
            int tilemapAddress = Convert.ToInt32(txtTilemapAddress.Text, 16);
            int width = Convert.ToInt32(txtWidth.Text);
            int height = Convert.ToInt32(txtHeight.Text);

            for (int i = 0; i < width * height * 2; ++i)
                MemMgr.WriteByte(tilemapAddress + i, 0);
        }

        private void on_btnSaveTileset_clicked(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Save Tilemap File", this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "Tilemap";
                    ff.AddPattern("*.data");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        using (FileStream dataFile = File.Create(filechooser.Filename, 0x800, FileOptions.SequentialScan))
                        {
                            int tilemapAddress = Convert.ToInt32(txtTilemapAddress.Text, 16);
                            int width = Convert.ToInt32(txtWidth.Text);
                            int height = Convert.ToInt32(txtHeight.Text);

                            for (int i = 0; i < width * height * 2; ++i)
                            {
                                byte value = MemMgr.ReadByte(tilemapAddress + i);
                                dataFile.WriteByte(value);
                            }

                            dataFile.Close();
                        }
                    }
                }
            }
        }

        private void cboLUT_SelectedIndexChanged(object sender, EventArgs e)
        {
            int tset = Convert.ToInt32(cboTileset.ActiveId);
            int tilesetBaseAddr = MemoryMap.TILESET_BASE_ADDR + tset * 4;

            int lut = Convert.ToInt32(cboLUT.ActiveId);
            byte ConfigRegister = (byte)((chkStride256.Active ? 8 : 0) + lut);

            MemMgr.WriteByte(tilesetBaseAddr + 3, ConfigRegister);

            daCanvas.QueueDraw();
        }

        private void txtTilesetAddress_TextChanged(object sender, EventArgs e)
        {
            int tset = Convert.ToInt32(cboTileset.ActiveId);
            int tilesetBaseAddr = MemoryMap.TILESET_BASE_ADDR + tset * 4;
            int newAddress = Convert.ToInt32(txtTilesetAddress.Text.Replace(":", ""), 16);

            int offsetAddress = newAddress - 0xB0_0000;
            if (offsetAddress > -1)
                MemMgr.WriteLong(tilesetBaseAddr, offsetAddress);
        }

        private void txtTilemapAddress_TextChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;
            int newAddress = Convert.ToInt32(txtTilemapAddress.Text.Replace(":", ""), 16);

            int offsetAddress = newAddress - 0xB0_0000;
            if (offsetAddress > -1)
                MemMgr.WriteLong(tilemapBaseAddr + 1, offsetAddress);
        }

        private void Width_TextChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;

            if (txtWidth.Text.Length > 0)
            {
                int newValue = Convert.ToInt32(txtWidth.Text) & 0x3FF;
                MemMgr.WriteWord(tilemapBaseAddr + 4, newValue);
            }
        }

        private void Height_TextChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;

            if (txtHeight.Text.Length > 0)
            {
                int newValue = Convert.ToInt32(txtHeight.Text) & 0x3FF;
                MemMgr.WriteWord(tilemapBaseAddr + 6, newValue);
            }
        }

        private void txtWindowX_TextChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;

            if (txtWindowX.Text.Length > 0)
            {
                int newValue = Convert.ToInt32(txtWindowX.Text) & 0x3FF;
                MemMgr.WriteWord(tilemapBaseAddr + 8, newValue);
            }
        }

        private void txtWindowY_TextChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;

            if (txtWindowY.Text.Length > 0)
            {
                int newValue = Convert.ToInt32(txtWindowY.Text) & 0x3FF;
                MemMgr.WriteWord(tilemapBaseAddr + 10, newValue);
            }
        }

        private void chkTilemapEnabled_CheckedChanged(object sender, EventArgs e)
        {
            int tilemapBaseAddr = MemoryMap.TILE_CONTROL_REGISTER_ADDR + selectedTilemap * 12;
            MemMgr.WriteByte(tilemapBaseAddr, (byte)(chkTilemapEnabled.Active ? 1 : 0));
        }

        private void cboTileset_SelectedIndexChanged(object sender, EventArgs e)
        {
            int tset = Convert.ToInt32(cboTileset.ActiveId);
            int tilesetBaseAddr = MemoryMap.TILESET_BASE_ADDR + tset * 4;
            int tilesetAddr = MemMgr.ReadLong(tilesetBaseAddr);
            txtTilesetAddress.Text = (tilesetAddr + 0xB0_0000).ToString("X6");

            int cfgReg = MemMgr.ReadByte(tilesetBaseAddr + 3);
            chkStride256.Active = (cfgReg & 8) != 0;
            cboLUT.ActiveId = (cfgReg & 7).ToString();
        }
    }
}
