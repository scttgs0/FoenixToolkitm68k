namespace FoenixCore.Processor.m68000
{
    public partial class CentralProcessingUnit
    {
        public int PC = 0;

        public RegisterAddress A0 = new();
        public RegisterAddress A1 = new();
        public RegisterAddress A2 = new();
        public RegisterAddress A3 = new();
        public RegisterAddress A4 = new();
        public RegisterAddress A5 = new();
        public RegisterAddress A6 = new();
        public RegisterAddress A7 = new();

        public RegisterData D0 = new();
        public RegisterData D1 = new();
        public RegisterData D2 = new();
        public RegisterData D3 = new();
        public RegisterData D4 = new();
        public RegisterData D5 = new();
        public RegisterData D6 = new();
        public RegisterData D7 = new();

        public CpuFlags Flags = new();

        private RegisterStackPointer _userStack = new();
        private RegisterStackPointer _systemStack = new();

        /// <summary>
        /// Wait state. When Wait is true, the CoreCpu will not exeucte instructions. It
        /// will service the IRQ, NMI, and ABORT lines. A hardware interrupt is required 
        /// to restart the CoreCpu.
        /// </summary>
        public bool Waiting;
    }
}
