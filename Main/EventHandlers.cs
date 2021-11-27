#if WINFORM
using System.Windows.Forms;


namespace FoenixCore
{
    public class TerminalKeyEventArgs : KeyPressEventArgs
    {
        public Keys Modifiers;

        public TerminalKeyEventArgs(char KeyChar, Keys Modifiers = Keys.None) : base(KeyChar)
        {
            this.Modifiers = Modifiers;
        }
    }
}

#endif
