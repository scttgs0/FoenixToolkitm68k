using System;
using System.Diagnostics;


namespace FoenixCore.Processor.m68000
{
    public class OpCode
    {
        public byte Value;
        public string Mnemonic;
        public AddressModes AddressMode;
        public delegate void ExecuteDelegate(byte Instruction, AddressModes AddressMode, int Signature);
        public event ExecuteDelegate ExecuteOp;
        public int Length8Bit;
        public Processor.Generic.Register ActionRegister = null;

        public OpCode(byte Value, string Mnemonic, int Length8Bit, Processor.Generic.Register ActionRegister, AddressModes Mode, ExecuteDelegate newDelegate)
        {
            this.Value = Value;
            this.Length8Bit = Length8Bit;
            this.ActionRegister = ActionRegister;
            this.Mnemonic = Mnemonic;
            AddressMode = Mode;
            ExecuteOp += newDelegate;

            Debug.WriteLine("public const int " + Mnemonic + "_" + Mode.ToString() + "=0x" + Value.ToString("X2") + ";");
        }

        public OpCode(byte Value, string Mnemonic, int Length, AddressModes Mode, ExecuteDelegate newDelegate)
        {
            this.Value = Value;
            Length8Bit = Length;
            this.Mnemonic = Mnemonic;
            AddressMode = Mode;
            ExecuteOp += newDelegate;

            Debug.WriteLine("public const int " + Mnemonic + "_" + Mode.ToString() + "=0x" + Value.ToString("X2") + ";");
        }

        public void Execute(int SignatureBytes)
        {
            if (ExecuteOp == null)
                throw new NotImplementedException("Tried to execute " + Mnemonic + " but it is not implemented.");

            ExecuteOp(Value, AddressMode, SignatureBytes);
        }

        public int Length
        {
            get
            {
                if (ActionRegister != null && ActionRegister.Width == 2)
                    return Length8Bit + 1;

                return Length8Bit;
            }
        }

        public override string ToString()
        {
            return Mnemonic + " " + AddressMode.ToString();
        }

        public string ToString(int Signature)
        {
            string sig;
            int Dn = 0;
            int An = 0;
            int Xn = 0;
            int d_8 = 0;
            int d_16 = 0;
            int bd = 0;
            int od = 0;
            int xxx = 0;

            if (Length == 3)
                sig = "$" + Signature.ToString("X4");
            else if (Length == 4)
                sig = "$" + Signature.ToString("X6");
            else
                sig = "$" + Signature.ToString("X2");

            string arg = AddressMode switch
            {
                AddressModes.DataRegisterDirect => $"{Dn}",
                AddressModes.AddressRegisterDirect => $"{An}",
                AddressModes.AddressRegisterIndirect => $"({An})",
                AddressModes.AddressRegisterIndirectPostincrement => $"({An})+",
                AddressModes.AddressRegisterIndirectPredecrement => $"-({An})",
                AddressModes.AddressRegisterIndirectDisplacement => $"({d_16},{An})",
                AddressModes.AddressRegisterIndirectIndex8bit => $"({d_8},{An},{Xn})",
                AddressModes.AddressRegisterIndirectIndexBase => $"({bd},{An},{Xn})",
                AddressModes.MemoryIndirectPostindexed => $"([{bd},{An}],{Xn},{od})",
                AddressModes.MemoryIndirectPreindexed => $"([{bd},{An},{Xn}],{od})",
                AddressModes.ProgramCounterIndirectDisplacement => $"({d_16},PC)",
                AddressModes.ProgramCounterIndirectIndex8bit => $"({d_8},PC,{Xn})",
                AddressModes.ProgramCounterIndirectIndexBase => $"({bd},PC,{Xn})",
                AddressModes.ProgramCounterMemoryIndirectPostindexed => $"([{bd},PC],{Xn},{od})",
                AddressModes.ProgramCounterMemoryIndirectPreindexed => $"([{bd},PC,{Xn}],{od})",
                AddressModes.AbsoluteShort => $"{xxx}.W",
                AddressModes.AbsoluteLong => $"{xxx}.L",
                AddressModes.Immediate => $"#{xxx}",
                _ => "",
            };

            return Mnemonic + " " + arg;
        }
    }
}
