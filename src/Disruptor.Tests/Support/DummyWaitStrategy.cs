using System.Threading;

namespace Disruptor.Tests.Support
{
    public class DummyWaitStrategy : IWaitStrategy
    {
        public int SignalAllWhenBlockingCalls { get; private set; }

        public long WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            return 0;
        }

        public void SignalAllWhenBlocking()
        {
            SignalAllWhenBlockingCalls++;
        }
    }
}
