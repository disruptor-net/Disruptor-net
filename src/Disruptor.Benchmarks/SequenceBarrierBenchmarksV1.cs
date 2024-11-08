using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

public class SequenceBarrierBenchmarksV1 : SequenceBarrierBenchmarks, IDisposable
{
    private readonly SequenceBarrierV1 _requesterSequenceBarrier;
    private readonly SequenceBarrierV1 _replierSequenceBarrier;
    private readonly Task _replierTask;
    private readonly ManualResetEventSlim _replierStarted = new();

    public SequenceBarrierBenchmarksV1()
    {
        _requesterSequenceBarrier = new SequenceBarrierV1(_requesterSequencer, _requesterSequencer.GetWaitStrategy().NewSequenceWaiter(null, new DependentSequenceGroup(_requesterSequencer.GetCursorSequence())));
        _replierSequenceBarrier = new SequenceBarrierV1(_replierSequencer, _replierSequencer.GetWaitStrategy().NewSequenceWaiter(null, new DependentSequenceGroup(_replierSequencer.GetCursorSequence())));

        _replierTask = Task.Run(RunReplier);
        _replierStarted.Wait();
    }

    public void Dispose()
    {
        _replierSequenceBarrier.CancelProcessing();
        _replierTask.Wait();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Run()
    {
        Run<ISequenceBarrierOptions.None>();
    }

    private void Run<T>()
        where T : ISequenceBarrierOptions
    {
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            var seq = _replierRingBuffer.Next();
            _replierRingBuffer[seq].Value = 123456;
            _replierRingBuffer.Publish(seq);

            SequenceWaitResult result;
            do
            {
                result = _requesterSequenceBarrier.WaitFor<T>(seq);
            }
            while (result.IsTimeout || result.UnsafeAvailableSequence < seq);

            BeforePublication();
        }
    }

    private void RunReplier()
    {
        RunReplier<ISequenceBarrierOptions.None>();
    }

    private void RunReplier<T>()
        where T : ISequenceBarrierOptions
    {
        _replierStarted.Set();

        try
        {
            var sequence = -1L;

            while (true)
            {
                sequence++;

                SequenceWaitResult result;
                do
                {
                    result = _replierSequenceBarrier.WaitFor<T>(sequence);
                }
                while (result.IsTimeout || result.UnsafeAvailableSequence < sequence);

                BeforePublication();

                var seq = _requesterRingBuffer.Next();
                _requesterRingBuffer[seq].Value = 123456;
                _requesterRingBuffer.Publish(seq);
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }

    private interface ISequenceBarrierOptions
    {
        internal struct None : ISequenceBarrierOptions
        {
        }

        internal struct IsDependentSequencePublished : ISequenceBarrierOptions
        {
        }

        public static ISequenceBarrierOptions Get(ISequencer sequencer, DependentSequenceGroup dependentSequences)
        {
            if (sequencer is SingleProducerSequencer)
            {
                // The SingleProducerSequencer increments the cursor sequence on publication so the cursor sequence
                // is always published.
                return new IsDependentSequencePublished();
            }

            if (!dependentSequences.DependsOnCursor)
            {
                // When the sequence barrier does not directly depend on the ring buffer cursor, the dependent sequence
                // is always published (the value is derived from other event processors which cannot process unpublished
                // sequences).
                return new IsDependentSequencePublished();
            }

            return new None();
        }
    }

    private sealed class SequenceBarrierV1
    {
        private readonly ISequencer _sequencer;
        private readonly ISequenceWaiter _waitStrategy;
        private CancellationTokenSource _cancellationTokenSource;

        public SequenceBarrierV1(ISequencer sequencer, ISequenceWaiter waitStrategy)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public DependentSequenceGroup DependentSequences => _waitStrategy.DependentSequences;

        public bool IsCancellationRequested => _cancellationTokenSource.IsCancellationRequested;

        public CancellationToken CancellationToken
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _cancellationTokenSource.Token;
        }

        public void ThrowIfCancellationRequested() => _cancellationTokenSource.Token.ThrowIfCancellationRequested();

        public ISequenceBarrierOptions GetSequencerOptions()
        {
            return ISequenceBarrierOptions.Get(_sequencer, _waitStrategy.DependentSequences);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
        public SequenceWaitResult WaitFor(long sequence)
        {
            return WaitFor<ISequenceBarrierOptions.None>(sequence);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
        public SequenceWaitResult WaitFor<TSequenceBarrierOptions>(long sequence)
            where TSequenceBarrierOptions : ISequenceBarrierOptions
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var availableSequence = _waitStrategy.DependentSequences.Value;
            if (availableSequence >= sequence)
            {
                if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
                    return availableSequence;

                return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
            }

            if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
            {
                return InvokeWaitStrategy(sequence);
            }

            return InvokeWaitStrategyAndWaitForPublishedSequence(sequence);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategy(long sequence)
        {
            return _waitStrategy.WaitFor(sequence, _cancellationTokenSource.Token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategyAndWaitForPublishedSequence(long sequence)
        {
            var waitResult = _waitStrategy.WaitFor(sequence, _cancellationTokenSource.Token);

            if (waitResult.UnsafeAvailableSequence >= sequence)
            {
                return _sequencer.GetHighestPublishedSequence(sequence, waitResult.UnsafeAvailableSequence);
            }

            return waitResult;
        }

        public void ResetProcessing()
        {
            // Not disposing the previous value should be fine because the CancellationTokenSource instance
            // has no finalizer and no unmanaged resources to release.

            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void CancelProcessing()
        {
            _cancellationTokenSource.Cancel();
            _waitStrategy.Cancel();
        }
    }
}
