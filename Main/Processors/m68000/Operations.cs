namespace FoenixCore.Processor.m68000
{
    public class Operations
    {
        private readonly CentralProcessingUnit cpu;

        /// <summary>
        /// Used for addressing modes that 
        /// </summary>

        public delegate void SimulatorCommandEvent(int EventID);
        public event SimulatorCommandEvent SimulatorCommand;

        public Operations(CentralProcessingUnit cpu)
        {
            this.cpu = cpu;
        }

        private void OnSimulatorCommand(int signature)
        {
            if (SimulatorCommand == null)
                return;

            SimulatorCommand(signature);
        }
    }
}
