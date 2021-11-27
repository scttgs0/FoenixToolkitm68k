using System;
using System.IO;
using System.Text;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;


namespace FoenixToolkit.UI
{
    class GameGeneratorWindow : Window
    {
#pragma warning disable CS0649  // never assigned
        //[GUI] Button btnGenerateASM;
        //[GUI] Button btnViewAssets;
        //[GUI] Button btnLoad;
        //[GUI] Button btnSave;
        //[GUI] Button btnClose;
        [GUI] TextView tvwCode;
#pragma warning restore CS0649

        public GameGeneratorWindow() : this(new Builder("GameGeneratorWindow.ui")) { }

        private GameGeneratorWindow(Builder builder) : base(builder.GetRawOwnedObject("GameGeneratorWindow"))
        {
            builder.Autoconnect(this);
            HideOnDelete();
        }

        private void on_btnLoad_clicked(object sender, EventArgs e)
        {
            FileChooserDialog filechooser =
                new("Foenix Game File to Open", this,
                    FileChooserAction.Open,
                    "Cancel", ResponseType.Cancel,
                    "Open", ResponseType.Accept);

            FileFilter ff = new();
            ff.Name = "FGM";
            ff.AddPattern("*.fgm");
            filechooser.AddFilter(ff);

            if (filechooser.Run() == (int)ResponseType.Accept) 
            {
                using (FileStream file = File.OpenRead(filechooser.Filename)) {
                    byte[] buffer = new byte[file.Length];
                    file.Read(buffer, 0, buffer.Length);

                    tvwCode.Buffer.Text = Encoding.ASCII.GetString(buffer);
                }                
            }

            ff.Dispose();
            filechooser.Destroy();
        }

        private void on_btnSave_clicked(object sender, EventArgs e)
        {
            using (FileChooserDialog filechooser =
                new("Save File", this,
                FileChooserAction.Save,
                "Cancel", ResponseType.Cancel,
                "Open", ResponseType.Accept))
            {
                using (FileFilter ff1 = new())
                {
                    ff1.Name = "FGM (*.fgm)";
                    ff1.AddPattern("*.fgm");
                    filechooser.AddFilter(ff1);
                }

                if (filechooser.Run() == (int)ResponseType.Accept)
                {
                    using (FileStream outputFile = File.Create(filechooser.Filename))
                    {
                        byte[] buffer = Encoding.ASCII.GetBytes(tvwCode.Buffer.Text);
                        outputFile.Write(buffer, 0, buffer.Length);
                    }
                }
            }
        }

        private void on_btnClose_clicked(object sender, EventArgs e)
        {
            Hide();
        }

        private void on_btnGenerateASM_clicked(object sender, EventArgs e)
        {
            // parse the code and generate .asm file(s)
        }

        private void on_btnViewAssets_clicked(object sender, EventArgs e)
        {
            // show the asset window
        }
    }
}
