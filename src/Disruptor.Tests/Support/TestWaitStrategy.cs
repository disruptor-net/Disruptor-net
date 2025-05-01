using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests.Support;

public class TestWaitStrategy : IWaitStrategy, IAsyncWaitStrategy
{
    private readonly Dictionary<IEventHandler, SequenceWaitResult> _nextWaitResults = new();

    public bool IsBlockingStrategy { get; set; }

    public void SetupNextSequence(IEventHandler eventHandler, SequenceWaitResult waitResult)
    {
        _nextWaitResults[eventHandler] = waitResult;
    }

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(this, eventHandler, dependentSequences);
    }

    public IAsyncSequenceWaiter NewAsyncSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        return new SequenceWaiter(this, eventHandler, dependentSequences);
    }

    public void SignalAllWhenBlocking()
    {
    }

    private class SequenceWaiter(TestWaitStrategy waitStrategy, IEventHandler? eventHandler, DependentSequenceGroup dependentSequences) : ISequenceWaiter, IAsyncSequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
        {
            return eventHandler != null
                ? waitStrategy._nextWaitResults.GetValueOrDefault(eventHandler)
                : new SequenceWaitResult(0);
        }

        public ValueTask<SequenceWaitResult> WaitForAsync(long sequence, CancellationToken cancellationToken)
        {
            return new ValueTask<SequenceWaitResult>(WaitFor(sequence, cancellationToken));
        }

        public void Cancel()
        {
        }

        public void Dispose()
        {
        }
    }
}
