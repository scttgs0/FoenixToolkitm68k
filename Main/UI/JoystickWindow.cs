using System;
using System.Drawing;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore.MemoryLocations;


namespace FoenixToolkit.UI
{
    class JoystickWindow : Window
    {
#pragma warning disable CS0649  // never assigned
        //[GUI] Gtk.Label lblDebug;
        [GUI] Gtk.Image imgA;
        [GUI] Gtk.Image imgB;
        [GUI] Gtk.Image imgUp;
        [GUI] Gtk.Image imgDown;
        [GUI] Gtk.Image imgLeft;
        [GUI] Gtk.Image imgRight;
#pragma warning restore CS0649

        public MemoryRAM gabe = null;

        public JoystickWindow() : this(new Builder("JoystickWindow.ui"))
        {
            // imgA.Events = imgA.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            // imgB.Events = imgB.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            // imgUp.Events = imgUp.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            // imgDown.Events = imgDown.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            // imgLeft.Events = imgLeft.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
            // imgRight.Events = imgRight.Events | Gdk.EventMask.ButtonPressMask | Gdk.EventMask.ButtonReleaseMask;
        }

        private JoystickWindow(Builder builder) : base(builder.GetRawOwnedObject("JoystickWindow"))
        {
            builder.Autoconnect(this);
            HideOnDelete();
        }

        private void SendJoystickValue(int joystick, byte value)
        {
            if (gabe != null)
                gabe.WriteByte(MemoryMap.JOYSTICK0 - MemoryMap.GABE_START + joystick, value);
        }

        private void updateImage(string name, bool isPressed)
        {
            switch (name)
            {
                case "evtA":
                    imgA.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/button-a-sm-active.png" : "./Images/button-a-sm.png");
                    break;

                case "evtB":
                    imgB.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/button-b-sm-active.png" : "./Images/button-b-sm.png");
                    break;

                case "evtUp":
                    imgUp.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/up-squared-sm-active.png" : "./Images/up-squared-sm.png");
                    break;

                case "evtDown":
                    imgDown.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/down-squared-sm-active.png" : "./Images/down-squared-sm.png");
                    break;

                case "evtLeft":
                    imgLeft.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/left-squared-sm-active.png" : "./Images/left-squared-sm.png");
                    break;

                case "evtRight":
                    imgRight.Pixbuf = new Gdk.Pixbuf(
                        isPressed ? "./Images/right-squared-sm-active.png" : "./Images/right-squared-sm.png");
                    break;
            }
        }

        private void on_JoystickWindow_key_press_event(object sender, KeyPressEventArgs e)
        {
            if (e.Event.Key == Gdk.Key.Escape)
                Hide();

            byte value = 0;
            switch (e.Event.Key)
            {
                case Gdk.Key.A:
                case Gdk.Key.a:
                    value = 0x9B;
                    updateImage("evtLeft", true);
                    break;

                case Gdk.Key.S:
                case Gdk.Key.s:
                    value = 0x9D;
                    updateImage("evtDown", true);
                    break;

                case Gdk.Key.D:
                case Gdk.Key.d:
                    value = 0x97;
                    updateImage("evtRight", true);
                    break;

                case Gdk.Key.W:
                case Gdk.Key.w:
                    value = 0x9E;
                    updateImage("evtUp", true);
                    break;

                case Gdk.Key.Q:
                case Gdk.Key.q:
                    value = 0x8F;
                    updateImage("evtA", true);
                    break;

                case Gdk.Key.E:
                case Gdk.Key.e:
                    value = 0x1F;
                    updateImage("evtB", true);
                    break;
            }

            if (value != 0)
                SendJoystickValue(0, value);
        }

        private void on_JoystickWindow_key_release_event(object sender, KeyReleaseEventArgs e)
        {
            SendJoystickValue(0, 0x9F);

            switch (e.Event.Key)
            {
                case Gdk.Key.A:
                case Gdk.Key.a:
                    updateImage("evtLeft", false);
                    break;

                case Gdk.Key.S:
                case Gdk.Key.s:
                    updateImage("evtDown", false);
                    break;

                case Gdk.Key.D:
                case Gdk.Key.d:
                    updateImage("evtRight", false);
                    break;

                case Gdk.Key.W:
                case Gdk.Key.w:
                    updateImage("evtUp", false);
                    break;

                case Gdk.Key.Q:
                case Gdk.Key.q:
                    updateImage("evtA", false);
                    break;

                case Gdk.Key.E:
                case Gdk.Key.e:
                    updateImage("evtB", false);
                    break;
            }
        }

        /*
         * All buttons use this event.
         */
        private void on_all_button_press_event(object sender, ButtonPressEventArgs e)
        {
            if (sender is Gtk.EventBox ctrl)
            {
                int buttonPressed = ctrl.Name switch
                {
                    "evtA" => 0x10,
                    "evtB" => 0x20,
                    "evtUp" => 0x01,
                    "evtDown" => 0x02,
                    "evtLeft" => 0x04,
                    "evtRight" => 0x08,
                    _ => 0x00
                };

                if (buttonPressed != 0x00)
                    updateImage(ctrl.Name, true);

                byte value = (byte)(0x9F & ~buttonPressed);
                SendJoystickValue(0, value);
            }
        }

        private void on_all_button_release_event(object sender, ButtonReleaseEventArgs e)
        {
            if (sender is Gtk.EventBox ctrl)
                updateImage(ctrl.Name, false);

            SendJoystickValue(0, 0x9F);
        }
    }
}
