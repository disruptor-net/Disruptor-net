using System.Threading;

namespace Disruptor.Tests.Support
{
    public class DummyNonBlockingWaitStrategy : INonBlockingWaitStrategy
    {
        public int SignalAllWhenBlockingCalls { get; private set; }

        public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            return 0;
        }

        public void SignalAllWhenBlocking()
        {
            SignalAllWhenBlockingCalls++;
        }
    }
}
