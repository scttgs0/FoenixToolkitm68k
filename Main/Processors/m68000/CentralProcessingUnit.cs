using System;
using System.Runtime.InteropServices;
using System.Threading;

using FoenixCore.MemoryLocations;


namespace FoenixCore.Processor.m68000
{
    public partial class CentralProcessingUnit //: MusashiCB
    {
        private readonly OpcodeList opcodes = null;

        public OpCode CurrentOpcode = null;

        public int SignatureBytes = 0;

        public Processor.Generic.CpuPins Pins = new();

        /// <summary>
        /// When true, the CoreCpu will not execute the next instruction. Used by the debugger
        /// to allow the user to analyze memory and the execution trace. 
        /// </summary>
        public bool DebugPause = false;

        /// <summary>
        /// Length of the currently executing opcode
        /// </summary>
        public int OpcodeLength;

        /// <summary>
        /// Number of clock cycles used by the currently exeucting instruction
        /// </summary>
        public int OpcodeCycles;

        /// <summary>
        ///  The virtual CoreCpu speed
        /// </summary>
        private int clockSpeed = 20_000_000;

        /// <summary>
        /// number of cycles since the last performance checkpopint
        /// </summary>
        private int clockCyles = 0;

        /// <summary>
        /// the number of cycles to pause at until the next performance checkpoint
        /// </summary>
        private long nextCycleCheck = long.MaxValue;

        /// <summary>
        /// The last time the performance was checked. 
        /// </summary>
        private DateTime checkStartTime = DateTime.Now;

        public MemoryManager MemMgr = null;
        public Thread CPUThread = null;

        public event Operations.SimulatorCommandEvent SimulatorCommand;

        public int ClockSpeed => clockSpeed;

        public uint[] Snapshot
        {
            get
            {
                uint[] snapshot = new uint[] {
                    get_reg(null, m68k_register_t.M68K_REG_PC),
                    get_reg(null, m68k_register_t.M68K_REG_A0),
                    get_reg(null, m68k_register_t.M68K_REG_A1),
                    get_reg(null, m68k_register_t.M68K_REG_A2),
                    get_reg(null, m68k_register_t.M68K_REG_A3),
                    get_reg(null, m68k_register_t.M68K_REG_A4),
                    get_reg(null, m68k_register_t.M68K_REG_A5),
                    get_reg(null, m68k_register_t.M68K_REG_A6),
                    get_reg(null, m68k_register_t.M68K_REG_A7),
                    get_reg(null, m68k_register_t.M68K_REG_D0),
                    get_reg(null, m68k_register_t.M68K_REG_D1),
                    get_reg(null, m68k_register_t.M68K_REG_D2),
                    get_reg(null, m68k_register_t.M68K_REG_D3),
                    get_reg(null, m68k_register_t.M68K_REG_D4),
                    get_reg(null, m68k_register_t.M68K_REG_D5),
                    get_reg(null, m68k_register_t.M68K_REG_D6),
                    get_reg(null, m68k_register_t.M68K_REG_D7),
                    get_reg(null, m68k_register_t.M68K_REG_USP),
                    get_reg(null, m68k_register_t.M68K_REG_SP),
                    get_reg(null, m68k_register_t.M68K_REG_SR)
                };

                return snapshot;
            }
        }

        public CentralProcessingUnit(MemoryManager mm)
        {
            MemMgr = mm;
            clockSpeed = 20_000_000;
            clockCyles = 0;

            Operations operations = new(this);
            operations.SimulatorCommand += Operations_SimulatorCommand;
            opcodes = new OpcodeList(operations, this);

            // TODO: Musashi.m68k_init();
            // TODO: Musashi.m68k_set_cpu_type((uint)Musashi.M68K_CPU_TYPE_68000);
            // TODO: Musashi.m68k_pulse_reset();
        }

        private void Operations_SimulatorCommand(int EventID)
        {
            switch (EventID)
            {
                case Processor.Generic.SimulatorCommands.WaitForInterrupt:
                    break;

                case Processor.Generic.SimulatorCommands.RefreshDisplay:
                    SimulatorCommand?.Invoke(EventID);
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Execute for n cycles, then return. This gives the host a chance to do other things in the meantime.
        /// </summary>
        /// <param name="Cycles"></param>
        public void ExecuteCycles(int Cycles)
        {
            ResetCounter(Cycles);

            while (clockCyles < nextCycleCheck && !DebugPause)
                ExecuteNext();
        }

        /// <summary>
        /// Execute a single 
        /// return true if an interrupt is present
        /// </summary>
        public bool ExecuteNext()
        {
            if (Pins.Ready_)
                return false;

            if (Pins.InterruptPinActive)
                if (ServiceHardwareInterrupt())
                    return true;

            if (Waiting)
                return false;

            // TODO - if pc > RAM size, then throw an exception
            CurrentOpcode = opcodes[MemMgr.RAM.ReadByte(PC)];
            OpcodeLength = CurrentOpcode.Length;
            OpcodeCycles = 1;
            SignatureBytes = ReadSignature(OpcodeLength, PC);

            PC += OpcodeLength;
            CurrentOpcode.Execute(SignatureBytes);
            clockCyles += OpcodeCycles;

            return false;
        }

        // Simulator State management 
        /// <summary>
        /// Pause the CoreCpu execution due to a STP instruction. The CoreCpu may only be restarted
        /// by the Reset pin. In the simulator, this will close the CoreCpu execution thread.
        /// Restart the CoreCpu by executing Reset() and then Run()
        /// </summary>
        public void Halt()
        {
            if (CPUThread != null)
            {
                if (CPUThread.ThreadState == ThreadState.Running)
                {
                    DebugPause = true;
                    CPUThread.Join(1000);
                }

                // TODO: Musashi.m68k_pulse_halt();

                CPUThread = null;
            }
        }

        public void Reset()
        {
            Pins.VectorPull = true;
            MemMgr.VectorPull = true;

            Flags.Value = 0;
            A0.Value = A1.Value = A2.Value = A3.Value = 0;
            A4.Value = A5.Value = A6.Value = A7.Value = 0;
            D0.Value = D1.Value = D2.Value = D3.Value = 0;
            D4.Value = D5.Value = D6.Value = D7.Value = 0;
            _userStack.Reset();
            _systemStack.Reset();

            PC = MemMgr.ReadWord(MemoryMap.VECTOR_ERESET);

            //-- Flags.IrqDisable = true;
            Pins.IRQ = false;
            Pins.VectorPull = false;
            MemMgr.VectorPull = false;
            Waiting = false;

            // TODO: var memory = new byte[MemoryManager.MaxAddress + 1];
            // MemMgr.CopyIntoBuffer(0, MemoryManager.MaxAddress, memory);

            // TODO: Musashi.g_ram = memory;
            // TODO: Musashi.m68k_pulse_reset();
        }

        public static uint get_reg(SWIGTYPE_p_void context, m68k_register_t reg) {
            // TODO: uint ret = Musashi.m68k_get_reg(context, reg);
            return 0x0000;
        }

        public static void set_reg(m68k_register_t reg, uint value) {
            // TODO: Musashi.m68k_set_reg(reg, value);
        }

        // public override uint m68k_read_memory_8(uint address) {
        //     return 0x11;
        // }

        // public override uint m68k_read_memory_16(uint address) {
        //     return 0x2233;
        // }

        // public override uint m68k_read_memory_32(uint address) {
        //     return 0x44556677;
        // }

        // public override uint m68k_read_immediate_16(uint address)
        // {
        //     return 0x8899;
        // }

        // public override uint m68k_read_immediate_32(uint address)
        // {
        //     return 0xAABBCCDD;
        // }

        // public override uint m68k_read_pcrelative_8(uint address)
        // {
        //     return 0x0021;
        // }

        // public override uint m68k_read_pcrelative_16(uint address)
        // {
        //     return 0x9876;
        // }

        // public override uint m68k_read_pcrelative_32(uint address)
        // {
        //     return 0x67543210;
        // }

        // public override uint m68k_read_disassembler_8(uint address)
        // {
        //     return 0x00;
        // }

        // public override uint m68k_read_disassembler_16(uint address)
        // {
        //     return 0x0000;
        // }

        // public override uint m68k_read_disassembler_32(uint address)
        // {
        //     return 0x00000000;
        // }

        // public override void m68k_write_memory_8(uint address, uint value) {
        //     // TODO:
        // }

        // public override void m68k_write_memory_16(uint address, uint value) {
        //     // TODO:
        // }

        // public override void m68k_write_memory_32(uint address, uint value) {
        //     // TODO:
        // }

        /// <summary>
        /// Fetch and decode the next instruction for debugging purposes
        /// </summary>
        public OpCode PreFetch()
        {
            return opcodes[MemMgr[PC]];
        }

        public int ReadSignature(int length, int pc)
        {
            return length switch
            {
                2 => MemMgr.RAM.ReadByte(pc + 1),
                3 => MemMgr.RAM.ReadWord(pc + 1),
                4 => MemMgr.RAM.ReadLong(pc + 1),
                _ => 0,
            };
        }

        /// <summary>
        /// Clock cycles used for performance counter. This will be periodically reset to zero
        /// as the throttling routine adjusts the system performance. 
        /// </summary>
        public int CycleCounter => clockCyles;

        #region support routines
        /// <summary>
        /// Gets the address pointed to by a pointer in the data bank.
        /// </summary>
        /// <param name="baseAddress"></param>
        /// <param name="Index"></param>
        /// <returns></returns>
        private int GetPointerLocal(int baseAddress, Processor.Generic.Register Index = null)
        {
            //-- int addr = DataBank.GetLongAddress(baseAddress);

            // if (Index != null)
            //     addr += Index.Value;

            // return addr;
            return 0;
        }

        /// <summary>
        /// Gets the address pointed to by a pointer in Direct page.
        /// be in the Direct Page. The address returned will be DBR+Pointer.
        /// </summary>
        /// <param name="baseAddress"></param>
        /// <param name="Index"></param>
        /// <returns></returns>
        private int GetPointerDirect(int baseAddress, Processor.Generic.Register Index = null)
        {
            //-- int addr = DirectPage.Value + baseAddress;

            // if (Index != null)
            //     addr += Index.Value;

            // int pointer = MemMgr.ReadWord(addr);

            // return DataBank.GetLongAddress(pointer);
            return 0;
        }

        /// <summary>
        /// Gets the address pointed to by a pointer referenced by a long address.
        /// </summary>
        /// <param name="baseAddress">24-bit address</param>
        /// <param name="Index"></param>
        /// <returns></returns>
        private int GetPointerLong(int baseAddress, Processor.Generic.Register Index = null)
        {
            //-- int addr = baseAddress;

            // if (Index != null)
            //     addr += Index.Value;

            // return DataBank.GetLongAddress(MemMgr.ReadWord(addr));
            return 0;
        }

        #endregion

        /// <summary>
        /// Change execution to anohter address in the same bank
        /// </summary>
        /// <param name="addr"></param>
        public void JumpShort(int addr)
        {
            PC = (PC & 0xFF_0000) + (addr & 0xFFFF);
        }

        /// <summary>
        /// Change execution to a 24-bit address
        /// </summary>
        /// <param name="addr"></param>
        public void JumpLong(int addr)
        {
            //ProgramBank.Value = addr >> 16;
            // PC.Value = addr;
            PC = addr;
        }

        public void JumpVector(int VectorAddress)
        {
            int addr = MemMgr.ReadWord(VectorAddress);

            //ProgramBank.Value = 0;
            //PC.Value = addr;
            PC = addr;
        }

          public static byte GetByte(int Value, int Offset)
        {
            if (Offset == 0)
                return (byte)(Value & 0xff);

            if (Offset == 1)
                return (byte)(Value >> 8 & 0xff);

            if (Offset == 2)
                return (byte)(Value >> 16 & 0xff);

            throw new Exception("Offset must be 0-2. Got " + Offset.ToString());
        }

        public void Push(int value, int bytes)
        {
            if (bytes < 1 || bytes > 3)
                throw new Exception("bytes must be between 1 and 3. Got " + bytes.ToString());

            //-- Stack.Value -= bytes;
            // MemMgr.Write(Stack.Value + 1, value, bytes);
        }

        public void Push(Processor.Generic.Register Reg, int Offset)
        {
            Push(Reg.Value + Offset, Reg.Width);
        }

        public void Push(Processor.Generic.Register Reg)
        {
            Push(Reg.Value, Reg.Width);
        }

        public int Pull(int bytes)
        {
            if (bytes < 1 || bytes > 3)
                throw new Exception("bytes must be between 1 and 3. got " + bytes.ToString());

            //-- int ret = MemMgr.Read(Stack.Value + 1, bytes);

            // Stack.Value += bytes;

            //return ret;
            return 0;
        }

        public void PullInto(Processor.Generic.Register Register)
        {
            Register.Value = Pull(Register.Width);
        }

        /// <summary>
        /// Service highest priority interrupt
        /// </summary>
        public bool ServiceHardwareInterrupt()
        {
            if (Pins.IRQ /*-- && !_flags.IrqDisable*/)
            {
                //DebugPause = true;
                Pins.IRQ = false;
                Interrupt(Processor.Generic.InterruptTypes.IRQ);

                return true;
            }
            else if (Pins.NMI)
            {
                DebugPause = true;
                Pins.NMI = false;
                Interrupt(Processor.Generic.InterruptTypes.NMI);

                return true;
            }
            else if (Pins.Abort)
            {
                DebugPause = true;
                Pins.Abort = false;
                Interrupt(Processor.Generic.InterruptTypes.ABORT);

                return true;
            }
            else if (Pins.Reset)
            {
                DebugPause = true;
                Pins.Reset = false;
                Interrupt(Processor.Generic.InterruptTypes.RESET);

                return true;
            }

            return false;
        }

        public void Interrupt(Processor.Generic.InterruptTypes T)
        {
            //debug
            //DebugPause = true;

            Push(PC & 0xFF_FFFF, 4);

            //-- Push(Flags);

            //--Flags.IrqDisable = true;

            int addr;

            switch (T)
            {
                case Processor.Generic.InterruptTypes.BRK:
                    addr = MemoryMap.VECTOR_BRK;
                    break;

                case Processor.Generic.InterruptTypes.ABORT:
                    addr = MemoryMap.VECTOR_ABORT;
                    break;

                case Processor.Generic.InterruptTypes.IRQ:
                    addr = MemoryMap.VECTOR_IRQ;
                    break;

                case Processor.Generic.InterruptTypes.NMI:
                    addr = MemoryMap.VECTOR_NMI;
                    break;

                case Processor.Generic.InterruptTypes.RESET:
                    addr = MemoryMap.VECTOR_RESET;
                    break;

                case Processor.Generic.InterruptTypes.COP:
                    addr = MemoryMap.VECTOR_COP;
                    break;

                default:
                    throw new Exception("Invalid interrupt type: " + T.ToString());
            }

            Waiting = false;

            JumpVector(addr);
        }

        public void ResetCounter(int maxCycles)
        {
            clockCyles = 0;
            nextCycleCheck = maxCycles;
            checkStartTime = DateTime.Now;
        }
    }
}
