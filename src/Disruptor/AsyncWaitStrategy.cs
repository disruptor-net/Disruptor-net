using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor;

/// <summary>
/// Blocking wait strategy that uses <c>Monitor.Wait</c> and <c>Monitor.PulseAll</c>.
/// </summary>
/// <remarks>
/// This strategy can be used when throughput and low-latency are not as important as CPU resources.
/// </remarks>
public sealed class AsyncWaitStrategy : IAsyncWaitStrategy
{
    private readonly List<TaskCompletionSource<bool>> _taskCompletionSources = new();
    private readonly object _gate = new();
    private bool _hasSyncWaiter;

    public bool IsBlockingStrategy => true;

    public ISequenceWaiter NewSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
    {
        _hasSyncWaiter = true;
        return new SequenceWaiter(this, dependentSequences);
    }

    public IAsyncSequenceWaiter NewAsyncSequenceWaiter(IEventHandler? eventHandler, DependentSequenceGroup dependentSequences)
        => new SequenceWaiter(this, dependentSequences);

    public void SignalAllWhenBlocking()
    {
        lock (_gate)
        {
            if (_hasSyncWaiter)
            {
                Monitor.PulseAll(_gate);
            }

            foreach (var completionSource in _taskCompletionSources)
            {
                completionSource.TrySetResult(true);
            }
            _taskCompletionSources.Clear();
        }
    }

    private SequenceWaitResult WaitFor(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        if (dependentSequences.CursorValue < sequence)
        {
            lock (_gate)
            {
                while (dependentSequences.CursorValue < sequence)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Monitor.Wait(_gate);
                }
            }
        }

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    private async ValueTask<SequenceWaitResult> WaitForAsync(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        while (dependentSequences.CursorValue < sequence)
        {
            await WaitForAsyncImpl(sequence, dependentSequences, cancellationToken).ConfigureAwait(false);
        }

        return dependentSequences.AggressiveSpinWaitFor(sequence, cancellationToken);
    }

    private async ValueTask WaitForAsyncImpl(long sequence, DependentSequenceGroup dependentSequences, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> tcs;

        lock (_gate)
        {
            if (dependentSequences.CursorValue >= sequence)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _taskCompletionSources.Add(tcs);
        }

        // Using cancellationToken in the await is not required because SignalAllWhenBlocking is always invoked by
        // the sequencer barrier after cancellation.

        await tcs.Task.ConfigureAwait(false);
    }

    private class SequenceWaiter(AsyncWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences) : ISequenceWaiter, IAsyncSequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
            => waitStrategy.WaitFor(sequence, dependentSequences, cancellationToken);

        public ValueTask<SequenceWaitResult> WaitForAsync(long sequence, CancellationToken cancellationToken)
            => waitStrategy.WaitForAsync(sequence, dependentSequences, cancellationToken);

        public void Cancel()
            => waitStrategy.SignalAllWhenBlocking();

        public void Dispose()
        {
        }
    }
}
