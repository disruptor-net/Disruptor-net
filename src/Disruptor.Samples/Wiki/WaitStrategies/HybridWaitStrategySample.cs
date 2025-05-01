using System.Threading;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.WaitStrategies;

public class HybridWaitStrategySample
{
    public static void Run()
    {
        var disruptor = new Disruptor<Event>(() => new Event(), 1024, new CustomHybridWaitStrategy());
        disruptor.HandleEventsWith(new Handler1()).Then(new Handler2());
        disruptor.Start();
    }

    public class Event
    {
    }

    public interface IHighPriorityHandler
    {
    }

    public class Handler1 : IEventHandler<Event>, IHighPriorityHandler
    {
        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler2 : IEventHandler<Event>
    {
        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
        }
    }

    public class CustomHybridWaitStrategy : IWaitStrategy
    {
        // The most aggressive wait strategy, which waits in a while loop.
        private readonly BusySpinWaitStrategy _busySpinWaitStrategy = new();
        // The least aggressive non-blocking wait strategy, which waits using SpinWait.
        private readonly SpinWaitWaitStrategy _spinWaitWaitStrategy = new();

        public bool IsBlockingStrategy
            => _busySpinWaitStrategy.IsBlockingStrategy || _spinWaitWaitStrategy.IsBlockingStrategy;

        public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
        {
            return owner.Handler is IHighPriorityHandler
                ? _busySpinWaitStrategy.NewSequenceWaiter(owner, dependentSequences)
                : _spinWaitWaitStrategy.NewSequenceWaiter(owner, dependentSequences);
        }

        public void SignalAllWhenBlocking()
        {
            // Both methods are empty, so they will be inlined and removed.
            // Also, because IsBlockingStrategy is false, SignalAllWhenBlocking is
            // not invoked by the sequencer.

            _busySpinWaitStrategy.SignalAllWhenBlocking();
            _spinWaitWaitStrategy.SignalAllWhenBlocking();
        }
    }
}
