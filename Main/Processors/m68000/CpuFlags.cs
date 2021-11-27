using System;

using FoenixCore.Processor.GenericNew;


namespace FoenixCore.Processor.m68000
{
    public partial class CpuFlags : Register<Int16>
    {
        // Supervisor Status Flags
        public bool TR1;
        public bool TR0;
        public bool Supervisor;
        public bool Master;
        public bool INT2;
        public bool INT1;
        public bool INT0;

        // User Status Flags
        public bool Extend;
        public bool Negative;
        public bool Zero;
        public bool Overflow;
        public bool Carry;

        public override Int16 Value
        {
            get => GetFlags(
                        TR1,
                        TR0,
                        Supervisor,
                        Master,
                        false,
                        INT2,
                        INT1,
                        INT0,
                        false,
                        false,
                        false,
                        Extend,
                        Negative,
                        Zero,
                        Overflow,
                        Carry);
            set => SetFlags(value);
        }

        public virtual int CarryBit
        {
            get => Carry ? 1 : 0;
        }

        public Int16 GetFlags(params bool[] flags)
        {
            Int16 bits = 0;

            for (int i = 0; i < flags.Length; ++i)
            {
                bits = (Int16)(bits << 1);

                if (flags[i])
                    bits = (Int16)(bits | 1);
            }

            return bits;
        }

        public void SetFlags(Int16 value)
        {
            TR1         = (value & 0x8000) != 0;
            TR0         = (value & 0x4000) != 0;
            Supervisor  = (value & 0x2000) != 0;
            Master      = (value & 0x1000) != 0;
            INT2        = (value & 0x0400) != 0;
            INT1        = (value & 0x0200) != 0;
            INT0        = (value & 0x0100) != 0;

            Extend      = (value & 0x0010) != 0;
            Negative    = (value & 0x0008) != 0;
            Zero        = (value & 0x0004) != 0;
            Overflow    = (value & 0x0002) != 0;
            Carry       = (value & 0x0001) != 0;
        }

        public override string ToString()
        {
            char[] s = new char[] {
                TR1 ? '1' : '-',
                TR0 ? '0' : '-',
                Supervisor ? 'S' : '-',
                Master ? 'M' : '-',
                '_',
                INT2 ? '2' : '-',
                INT1 ? '1' : '-',
                INT0 ? '0' : '-',
                ' ',
                '_',
                '_',
                '_',
                Extend ? 'X' : '-',
                Negative ? 'N' : '-',
                Zero ? 'Z' : '-',
                Overflow ? 'V' : '-',
                Carry ? 'C' : '-'
            };

            return new string(s);
        }

        public void SetZ(int Val)
        {
            Zero = Val == 0;
        }

        public void SetZ(Processor.Generic.Register X)
        {
            Zero = X.Value == 0;
        }

        public void SetNZ(int Value, int Width)
        {
            Zero = (Width == 1 ? Value & 0xFF : Value & 0xFFFF) == 0;

            if (Width == 1)
                Negative = (Value & 0x80) != 0;
            else if (Width == 2)
                Negative = (Value & 0x8000) != 0;
        }

        public void Reset()
        {
            TR1         = false;
            TR0         = false;
            Supervisor  = false;
            Master      = false;
            INT2        = false;
            INT1        = false;
            INT0        = false;

            Extend      = false;
            Negative    = false;
            Zero        = false;
            Overflow    = false;
            Carry       = false;
        }
    }
}
