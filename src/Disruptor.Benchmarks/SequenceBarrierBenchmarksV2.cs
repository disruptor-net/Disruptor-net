using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

public class SequenceBarrierBenchmarksV2 : SequenceBarrierBenchmarks, IDisposable
{
    private readonly CustomSequenceBarrier _requesterSequenceBarrier;
    private readonly CustomSequenceBarrier _replierSequenceBarrier;
    private readonly Task _replierTask;
    private readonly ManualResetEventSlim _replierStarted = new();

    public SequenceBarrierBenchmarksV2()
    {
        _requesterSequenceBarrier = new CustomSequenceBarrier(_requesterSequencer, _requesterSequencer.GetWaitStrategy(), _requesterSequencer.GetCursorSequence(), Array.Empty<Sequence>());
        _replierSequenceBarrier = new CustomSequenceBarrier(_replierSequencer, _replierSequencer.GetWaitStrategy(), _replierSequencer.GetCursorSequence(), Array.Empty<Sequence>());

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
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            var seq = _replierRingBuffer.Next();
            _replierRingBuffer[seq].Value = 123456;
            _replierRingBuffer.Publish(seq);

            SequenceWaitResult result;
            do
            {
                result = _requesterSequenceBarrier.WaitFor<ISequenceBarrierOptions.None>(seq);
            }
            while (result.IsTimeout || result.UnsafeAvailableSequence < seq);

            BeforePublication();
        }
    }

    private void RunReplier()
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
                    result = _replierSequenceBarrier.WaitFor<ISequenceBarrierOptions.None>(sequence);
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

    private sealed class CustomSequenceBarrier
    {
        private readonly ISequencer _sequencer;
        private readonly IWaitStrategy _waitStrategy;
        private readonly DependentSequenceGroup _dependentSequences;
        private CancellationTokenSource _cancellationTokenSource;

        public CustomSequenceBarrier(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, Sequence[] dependentSequences)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _dependentSequences = new DependentSequenceGroup(cursorSequence, dependentSequences);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
        public SequenceWaitResult WaitFor<TSequenceBarrierOptions>(long sequence)
            where TSequenceBarrierOptions : ISequenceBarrierOptions
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var availableSequence = _dependentSequences.Value;
            if (availableSequence >= sequence)
            {
                if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
                    return availableSequence;

                return WaitHighestPublishedSequence(sequence, availableSequence);
            }

            if (typeof(TSequenceBarrierOptions) == typeof(ISequenceBarrierOptions.IsDependentSequencePublished))
            {
                return InvokeWaitStrategy(sequence);
            }

            return InvokeWaitStrategyAndWaitForPublishedSequence(sequence);
        }

        private SequenceWaitResult WaitHighestPublishedSequence(long sequence, long availableSequence)
        {
            const int maxSpinCount = 350;

            var spinCount = 0;

            while (!_sequencer.IsAvailable(sequence))
            {
                if (spinCount >= maxSpinCount)
                {
                    return sequence - 1;
                }

                spinCount++;
            }

            return _sequencer.GetHighestPublishedSequence(sequence, availableSequence);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategy(long sequence)
        {
            return _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategyAndWaitForPublishedSequence(long sequence)
        {
            var waitResult = _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);

            if (waitResult.UnsafeAvailableSequence >= sequence)
            {
                return WaitHighestPublishedSequence(sequence, waitResult.UnsafeAvailableSequence);
            }

            return waitResult;
        }

        public void CancelProcessing()
        {
            _cancellationTokenSource.Cancel();
            _waitStrategy.SignalAllWhenBlocking();
        }
    }
}
