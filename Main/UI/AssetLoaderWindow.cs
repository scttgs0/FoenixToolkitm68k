using System;
using System.Collections.Generic;
using System.IO;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore.MemoryLocations;
using FoenixCore.Simulator.FileFormat;


namespace FoenixToolkit.UI
{
    class AssetLoaderWindow : Window
    {
        public MemoryManager MemMgrRef = null;
        public ResourceChecker ResChecker;
        public int topLeftPixelColor = 0;

#pragma warning disable CS0649  // never assigned
        [GUI] Button btnStore;
        [GUI] ComboBoxText cboFileTypes;
        [GUI] ComboBoxText cboLUT;
        [GUI] FileChooserButton fcbBrowseFile;
        [GUI] Entry txtLoadAddress;
        [GUI] Label lblFileSizeResult;
#pragma warning restore CS0649

        public AssetLoaderWindow() : this(new Builder("AssetLoaderWindow.ui")) { }

        private AssetLoaderWindow(Builder builder) : base(builder.GetRawOwnedObject("AssetLoaderWindow"))
        {
            builder.Autoconnect(this);
            HideOnDelete();
        }

        private String FormatAddress(int address)
        {
            string size = address.ToString("X6");
            return $"${size[..2]}:{size[2..]}";
        }

        private byte HighByte(int value)
        {
            return ((byte)(value >> 16));
        }

        private byte MidByte(int value)
        {
            return ((byte)((value >> 8) & 0xFF));
        }

        private byte LowByte(int value)
        {
            return ((byte)(value & 0xFF));
        }

        /*
         * Convert a bitmap with no palette to a bytes with a color lookup table.
         */
        private void TransformBitmap(byte[] data, int startOffset, int pixelDepth, int lutPointer, int videoPointer, int width, int height)
        {
            List<int> lut = new(256)
            {
                // Always add black and white
                0,
                0xFFFFFF
            };

            // Read every pixel into a color table
            int bytes = pixelDepth switch
            {
                16 => 2,
                24 => 3,
                _ => 1
            };

            // Now read the bitmap
            for (int y = 0; y < height; ++y)
            {
                for (int x = 0; x < width; ++x)
                {
                    int pointer = startOffset + ((height - y - 1) * width + x) * bytes;
                    int rgb = pixelDepth switch
                    {
                        16 => (data[pointer] & 0x1F) + ((((data[pointer] & 0xE0) >> 5) + (data[pointer + 1] & 0x3) << 3) << 8) + ((data[pointer + 1] & 0x7C) << 14),
                        24 => data[pointer] + (data[pointer + 1] << 8) + (data[pointer + 2] << 16),
                        _ => -1
                    };

                    if (rgb != -1)
                    {
                        int index = lut.IndexOf(rgb);
                        byte value = (byte)index;

                        if (index == -1 && lut.Count < 256)
                        {
                            lut.Add(rgb);
                            value = (byte)(lut.Count - 1);

                            // Write the value to the LUT
                            MemMgrRef.WriteByte(value * 4 + lutPointer, data[pointer]);
                            MemMgrRef.WriteByte(value * 4 + 1 + lutPointer, data[pointer + 1]);
                            MemMgrRef.WriteByte(value * 4 + 2 + lutPointer, data[pointer + 2]);
                            MemMgrRef.WriteByte(value * 4 + 3 + lutPointer, 0xFF);
                        }

                        MemMgrRef.WriteByte(videoPointer++, value);
                    }
                }
            }
        }

        private void on_AssetLoaderWindow_realize(object sender, EventArgs e)
        {
            // Add items to the combo box
            // Tiles Registers: $AF:0100 to $AF:013F

            int index = 0;
            cboFileTypes.Append(index++.ToString(), "Bitmap Layer 0");
            cboFileTypes.Append(index++.ToString(), "Bitmap Layer 1");

            for (int i = 0; i < 4; ++i)
                cboFileTypes.Append(index++.ToString(), $"Tilemap {i}");

            for (int i = 0; i < 8; ++i)
                cboFileTypes.Append(index++.ToString(), $"Tileset {i}");

            for (int i = 0; i < 64; ++i)
                cboFileTypes.Append(index++.ToString(), $"Sprite {i}");

            for (int i = 0; i < 4; ++i)
                cboFileTypes.Append(index++.ToString(), $"LUT {i}");

            cboFileTypes.ActiveId = "0";

            index = 0;
            for (int i = 0; i < 4; ++i)
                cboLUT.Append(index++.ToString(), $"LUT {i}");

            cboLUT.ActiveId = "0";
            cboLUT.Sensitive = false;

            // initialize filters for the file browser
            FileFilter ff1 = new();
            ff1.Name = "Images Files";
            ff1.AddPattern("*.bin");
            ff1.AddPattern("*.bmp");
            ff1.AddPattern("*.data");
            ff1.AddPattern("*.pal");

            FileFilter ff2 = new();
            ff2.Name = "Binary Files";
            ff2.AddPattern("*.bin");

            FileFilter ff3 = new();
            ff3.Name = "Palette Files";
            ff3.AddPattern("*.pal");

            FileFilter ff4 = new();
            ff4.Name = "Bitmap Images";
            ff4.AddPattern("*.bmp");

            FileFilter ff5 = new();
            ff5.Name = "Data Files";
            ff5.AddPattern("*.data");

            FileFilter ff6 = new();
            ff6.Name = "All Files";
            ff6.AddPattern("*.*");

            fcbBrowseFile.AddFilter(ff1);
            fcbBrowseFile.AddFilter(ff2);
            fcbBrowseFile.AddFilter(ff3);
            fcbBrowseFile.AddFilter(ff4);
            fcbBrowseFile.AddFilter(ff5);
            fcbBrowseFile.AddFilter(ff6);
        }

        private void on_AssetLoaderWindow_unrealize(object sender, EventArgs e)
        {
            foreach (FileFilter ff in fcbBrowseFile.Filters)
            {
                fcbBrowseFile.RemoveFilter(ff);
                ff.Dispose();
            }
        }

        private void on_AssetLoaderWindow_key_press_event(object sender, KeyPressEventArgs e)
        {
            if (e.Event.Key == Gdk.Key.Escape)
                Close();
        }

        private void on_cboFileTypes_changed(object sender, EventArgs e)
        {
            bool LUTSelected = cboFileTypes.ActiveText.StartsWith("LUT");

            cboLUT.Sensitive = cboFileTypes.ActiveId != "0" && !LUTSelected;
            txtLoadAddress.Sensitive = !LUTSelected;

            if (LUTSelected)
            {
                int lut = Convert.ToInt32(cboFileTypes.ActiveText[4..]);
                txtLoadAddress.Sensitive = false;
                txtLoadAddress.Text = (MemoryMap.GRP_LUT_BASE_ADDR + lut * 1024).ToString("X6");
            }
        }

        /*
         * Let the user select a file from the file system and display it in a text box.
         */
        private void on_fcbBrowseFile_file_set(object sender, EventArgs e)
        {
            FileInfo info = new(fcbBrowseFile.Filename);
            lblFileSizeResult.Text = FormatAddress((int)info.Length);
            btnStore.Sensitive = true;
        }

        private void on_btnStore_clicked(object sender, EventArgs e)
        {
            btnStore.Sensitive = false;

            // Store the address in the pointer address - little endian - 24 bits
            int destAddress = Convert.ToInt32(txtLoadAddress.Text.Replace(":", ""), 16);

            byte[] data = File.ReadAllBytes(fcbBrowseFile.Filename);

            for (int i = 0; i < data.Length; ++i)
                MemMgrRef.WriteByte(destAddress + i, data[i]);

            // Determine which addresses to store the bitmap into.
            int ftVal = Convert.ToInt32(cboFileTypes.ActiveId);
            byte lutVal = Convert.ToByte(cboLUT.ActiveId);

            if (ftVal == 0)
            {
                // Raw
            }
            else if (ftVal < 5)
            {
                // Tilemaps 4
                int tilemapIndex = ftVal - 1;
                int baseAddress = MemoryMap.TILE_CONTROL_REGISTER_ADDR + tilemapIndex * 12;

                // enable the tilemap
                MemMgrRef.WriteByte(baseAddress, (byte)(1 + (lutVal << 1)));

                // write address offset by bank $b0
                int offsetAddress = destAddress - 0xB0_0000;
                MemMgrRef.WriteByte(baseAddress + 1, (byte)(offsetAddress & 0xFF));
                MemMgrRef.WriteByte(baseAddress + 2, (byte)((offsetAddress & 0xFF00) >> 8));
                MemMgrRef.WriteByte(baseAddress + 3, (byte)((offsetAddress & 0xFF_0000) >> 16));
                // TODO: Need to write the size of the tilemap
            }
            else if (ftVal < 13)
            {
                // Tilesets 8
                int tilesetIndex = ftVal - 5;
                int baseAddress = MemoryMap.TILESET_BASE_ADDR + tilesetIndex * 4;

                // write address offset by bank $b0
                int offsetAddress = destAddress - 0xB0_0000;
                MemMgrRef.WriteByte(baseAddress, (byte)(offsetAddress & 0xFF));
                MemMgrRef.WriteByte(baseAddress + 1, (byte)((offsetAddress & 0xFF00) >> 8));
                MemMgrRef.WriteByte(baseAddress + 2, (byte)((offsetAddress & 0xFF_0000) >> 16));
                MemMgrRef.WriteByte(baseAddress + 3, lutVal);  // TODO: Add the stride 256 bit 3.
            }
            else
            {
                // Sprites 64
                int spriteIndex = ftVal - 13;
                int baseAddress = MemoryMap.SPRITE_CONTROL_REGISTER_ADDR + spriteIndex * 8;

                // enable the tilemap
                MemMgrRef.WriteByte(baseAddress, (byte)(1 + (lutVal << 1)));  // TODO: Add sprite depth

                // write address offset by bank $b0
                int offsetAddress = destAddress - 0xB0_0000;
                MemMgrRef.WriteByte(baseAddress + 1, (byte)(offsetAddress & 0xFF));
                MemMgrRef.WriteByte(baseAddress + 2, (byte)((offsetAddress & 0xFF00) >> 8));
                MemMgrRef.WriteByte(baseAddress + 3, (byte)((offsetAddress & 0xFF_0000) >> 16));
                // TODO: set the position of the sprite
            }

            ResourceChecker.Resource res = new()
            {
                StartAddress = destAddress,
                SourceFile = fcbBrowseFile.Filename,
                Name = System.IO.Path.GetFileNameWithoutExtension(fcbBrowseFile.Filename),
                FileType = (ResourceChecker.ResourceType)ftVal  // TODO: Bug???
            };

            btnStore.Sensitive = true;
        }
    }
}
