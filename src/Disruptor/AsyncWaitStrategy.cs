using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#if DISRUPTOR_V5

namespace Disruptor
{
    public class AsyncWaitStrategy : IAsyncWaitStrategy
    {
        private readonly List<TaskCompletionSource<bool>> _taskCompletionSources = new List<TaskCompletionSource<bool>>();
        private readonly object _gate = new object();
        private readonly IWaitStrategy _waitStrategy;

        public AsyncWaitStrategy()
            : this(new BlockingSpinWaitWaitStrategy())
        {
        }

        public AsyncWaitStrategy(IWaitStrategy waitStrategy)
        {
            _waitStrategy = waitStrategy;
        }

        public bool IsBlockingStrategy => true;

        public SequenceWaitResult WaitFor(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            return _waitStrategy.WaitFor(sequence, cursor, dependentSequence, cancellationToken);
        }

        public void SignalAllWhenBlocking()
        {
            _waitStrategy.SignalAllWhenBlocking();

            lock (_gate)
            {
                foreach (var completionSource in _taskCompletionSources)
                {
                    completionSource.TrySetResult(true);
                }
                _taskCompletionSources.Clear();
            }
        }

        public async ValueTask<SequenceWaitResult> WaitForAsync(long sequence, Sequence cursor, ISequence dependentSequence, CancellationToken cancellationToken)
        {
            while (cursor.Value < sequence)
            {
                await WaitForAsyncImpl(sequence, cursor, cancellationToken);
            }

            var aggressiveSpinWait = new AggressiveSpinWait();
            long availableSequence;
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                cancellationToken.ThrowIfCancellationRequested();
                aggressiveSpinWait.SpinOnce();
            }

            return availableSequence;
        }

        private async ValueTask WaitForAsyncImpl(long sequence, Sequence cursor, CancellationToken cancellationToken)
        {
            TaskCompletionSource<bool> tcs;

            lock (_gate)
            {
                if (cursor.Value >= sequence)
                {
                    return;
                }

                tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _taskCompletionSources.Add(tcs);
            }

            using var x = cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(false), tcs);

            var result = await tcs.Task;
            if (!result)
                cancellationToken.ThrowIfCancellationRequested();
        }
    }
}

#endif
