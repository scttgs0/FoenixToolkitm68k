using System;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;


namespace FoenixToolkit.UI
{
    class SerialTerminalWindow : Window
    {
#pragma warning disable CS0649  // never assigned
        [GUI] TextView tvwContent;
#pragma warning restore CS0649

        public static SerialTerminalWindow Instance;

        public SerialTerminalWindow() : this(new Builder("SerialTerminalWindow.ui")) { }

        private SerialTerminalWindow(Builder builder) : base(builder.GetRawOwnedObject("SerialTerminalWindow"))
        {
            Instance = this;
            builder.Autoconnect(this);
            HideOnDelete();
        }

        public void AppendContent(string data)
        {
            tvwContent.Buffer.Text += data;
        }

        public void AppendContent(char data)
        {
            tvwContent.Buffer.Text += data;
        }
    }
}
