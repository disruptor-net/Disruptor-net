namespace Disruptor.Tests
{
    public class DummySequenceBarrier : ISequenceBarrier
    {
        public long WaitFor(long sequence)
        {
            return 0;
        }

        public long Cursor => 0;
        public bool IsAlerted => false;
        public void Alert()
        {
        }

        public void ClearAlert()
        {
        }

        public void CheckAlert()
        {
        }
    }
}