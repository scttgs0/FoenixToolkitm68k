using System;

using Gtk;
using GUI = Gtk.Builder.ObjectAttribute;

using FoenixCore.Processor.Generic;


namespace FoenixToolkit.UI
{
    public class RegisterControl : Box
    {
        string _caption;
        string _value;
        Register _register = null;
        RegisterBankNumber _bank = null;

#pragma warning disable CS0649  // never assigned
        [GUI] Label lblRegister;
        [GUI] Entry txtRegister;
#pragma warning restore CS0649

        public RegisterControl() : this(new Builder("RegisterControl.ui")) { }

        private RegisterControl(Builder builder) : base(builder.GetRawOwnedObject("RegisterControl"))
        {
            builder.Autoconnect(this);
        }

        public string Caption
        {
            get => _caption;
            set
            {
                _caption = value;
                lblRegister.Text = value;
            }
        }

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                txtRegister.Text = value;
            }
        }

        public Register Register
        {
            get => _register;
            set
            {
                _register = value;
                if (value != null)
                    UpdateValue();
            }
        }

        public void UpdateValue()
        {
            if (Bank != null && Register != null)
                Value = Bank.Value.ToString("X2") + _register.Value.ToString("X4");
            else if (Register != null)
                Value = _register.ToString();
        }

        public RegisterBankNumber Bank
        {
            get => _bank;
            set
            {
                _bank = value;
                UpdateValue();
            }
        }
    }
}
