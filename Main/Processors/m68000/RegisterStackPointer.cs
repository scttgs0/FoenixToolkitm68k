namespace FoenixCore.Processor.m68000
{
    public class RegisterStackPointer : Processor.GenericNew.Register<int>
    {
        private const int defaultValue = unchecked((int) 0x18000);
        private int topOfStack = defaultValue;

        public void Reset()
        {
            topOfStack = defaultValue;
            _value = defaultValue;
        }
    }
}
