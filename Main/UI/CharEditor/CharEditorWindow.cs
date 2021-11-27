using System;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;


namespace FoenixToolkit.UI
{
    class CharEditorWindow : Window
    {
#pragma warning disable CS0649  // never assigned
        [GUI] Fixed fixLeft;
        [GUI] Fixed fixRightBottom;
        [GUI] Label lblSelectedIndex;
        [GUI] CharViewerControl ucCharViewer;
        [GUI] CharEditControl ucCharEdit;
#pragma warning restore CS0649

        public CharEditorWindow() : this(new Builder("CharEditorWindow.ui"))
        {
            ucCharViewer = new();
            fixLeft.Add(ucCharViewer);

            ucCharEdit = new();
            fixRightBottom.Add(ucCharEdit);
        }

        private CharEditorWindow(Builder builder) : base(builder.GetRawOwnedObject("CharEditorWindow"))
        {
            builder.Autoconnect(this);
            HideOnDelete();
        }

        private void on_CharEditorWindow_realize(object sender, EventArgs e)
        {
            ucCharViewer.CharacterSelected += on_ucCharViewer_CharacterSelected;
            ucCharEdit.CharacterSaved += on_ucCharEdit_CharacterSaved;
        }

        private void CharEditorWindow_KeyDown(object sender, KeyPressEventArgs e)
        {
            if (e.Event.Key == Gdk.Key.Escape)
                Close();
        }

        private void on_menuFileNew_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        private void on_menuFileOpen_activate(object sender, EventArgs e) {
            using (FileChooserDialog filechooser =
                new("Load File", this,
                FileChooserAction.Open,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                FileFilter ff1 = new();
                ff1.Name = "Image Files";
                ff1.AddPattern("*.bin");
                ff1.AddPattern("*.bmp");
                ff1.AddPattern("*.png");

                FileFilter ff2 = new();
                ff2.Name = "ROM file";
                ff2.AddPattern("*.bin");

                FileFilter ff3 = new();
                ff3.Name = "PNG Image";
                ff3.AddPattern("*.png");

                FileFilter ff4 = new();
                ff4.Name = "BMP Image";
                ff4.AddPattern("*.bmp");

                FileFilter ff5 = new();
                ff5.Name = "All Files";
                ff5.AddPattern("*.*");

                filechooser.AddFilter(ff1);
                filechooser.AddFilter(ff2);
                filechooser.AddFilter(ff3);
                filechooser.AddFilter(ff4);
                filechooser.AddFilter(ff5);

                if (filechooser.Run() == (int)ResponseType.Accept)
                {
                    string ext = System.IO.Path.GetExtension(filechooser.Filename).ToLower();
                    switch (ext)
                    {
                        case ".bin":
                            ucCharViewer.FontData = ucCharViewer.LoadBin(filechooser.Filename);
                            break;

                        case ".png":
                        case ".bmp":
                            ucCharViewer.FontData = ucCharViewer.LoadPNG(filechooser.Filename);
                            break;
                    }

                    ucCharViewer.QueueDraw();
                }

                ff5.Dispose();
                ff4.Dispose();
                ff3.Dispose();
                ff2.Dispose();
                ff1.Dispose();
            }
        }

        private void on_menuFileSave_activate(object sender, EventArgs e) {
            using (FileChooserDialog filechooser =
                new("Save File", this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                using (FileFilter ff = new())
                {
                    ff.Name = "ROM file";
                    ff.AddPattern("*.bin");
                    filechooser.AddFilter(ff);

                    if (filechooser.Run() == (int)ResponseType.Accept)
                    {
                        string ext = System.IO.Path.GetExtension(filechooser.Filename).ToLower();
                        switch (ext)
                        {
                            case ".bin":
                                ucCharViewer.SaveBin(filechooser.Filename, ucCharViewer.FontData);
                                break;

                            case ".png":
                            case ".bmp":
                                // ucCharViewer.InputData = ucCharViewer.LoadPNG(filechooser.Filename);
                                using (var md = new MessageDialog(null, DialogFlags.Modal | DialogFlags.DestroyWithParent,
                                        MessageType.Error, ButtonsType.Ok, "Saving to PNG and BMP not implemented yet")) {
                                    md.Title = "Not Implemented";
                                    md.Run();
                                }
                                break;
                        }

                        ucCharViewer.QueueDraw();
                    }
                }
            }
        }

        private void on_menuFileSaveAs_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        private void on_menuFileQuit_activate(object sender, EventArgs e) {
            Hide();
        }

        private void on_menuEditCut_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        private void on_menuEditCopy_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        private void on_menuEditPaste_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        private void on_menuEditDelete_activate(object sender, EventArgs e) {
            throw new NotImplementedException();
        }

        /**
         * This method is called when a cell is selected in the ViewControl
         */
        private void on_ucCharViewer_CharacterSelected(object sender, EventArgs e)
        {
            int value = ucCharViewer.SelectedIndex;
            lblSelectedIndex.Text = $"Dec: {value}, Hex: ${value.ToString("X2")}, Char: {Convert.ToChar(value)}";

            ucCharEdit.LoadCharacter(ucCharViewer.FontData, value, ucCharViewer.BytesPerCharacter);
        }

        private void on_ucCharEdit_CharacterSaved(object sender, EventArgs e)
        {
            ucCharViewer.QueueDraw();
        }
    }
}
