using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Disruptor.Util;

namespace Disruptor.Benchmarks.Processing;

/// <summary>
/// Use partial copies of the processor types to benchmark the WaitFor invocation.
/// </summary>
public class EventProcessorBenchmarks_Wait
{
    private const int _operationsPerInvoke = 1000;

    private readonly IPartialEventProcessor _processor1;
    private readonly IPartialEventProcessor _processor2;

    public EventProcessorBenchmarks_Wait()
    {
        var waitStrategy = new YieldingWaitStrategy();
        var sequencer = new SingleProducerSequencer(64, waitStrategy);
        var cursorSequence = new Sequence();
        var dependentSequences = new Sequence[0];
        var sequenceBarrier = new SequenceBarrier(sequencer, waitStrategy, cursorSequence, dependentSequences);
        var sequenceBarrierClass = new SequenceBarrierClass(sequencer, waitStrategy, cursorSequence, dependentSequences);
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrierClass);
        var eventProcessorType = typeof(PartialEventProcessor<,>).MakeGenericType(typeof(ISequenceBarrierOptions.IsDependentSequencePublished), sequenceBarrierProxy.GetType());
        _processor1 = (IPartialEventProcessor)Activator.CreateInstance(eventProcessorType, sequenceBarrier, sequenceBarrierProxy);

        _processor2 = new PartialEventProcessor<ISequenceBarrierOptions.IsDependentSequencePublished, SequenceBarrierStruct>(sequenceBarrier, new SequenceBarrierStruct(sequencer, waitStrategy, cursorSequence, dependentSequences));

        sequencer.Publish(42);
        cursorSequence.SetValue(42);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_Default()
    {
        _processor1.ProcessingLoop_Options(42);
    }

    [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_Typed_Class_Proxy()
    {
        _processor1.ProcessingLoop_Typed(42);
    }

    [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_Typed_Struct()
    {
        _processor2.ProcessingLoop_Typed(42);
    }

    public interface IPartialEventProcessor
    {
        void ProcessingLoop_Options(long nextSequence);
        void ProcessingLoop_Typed(long nextSequence);
    }

    /// <summary>
    /// Partial copy of <see cref="EventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler, TBatchStartAware}"/>
    /// </summary>
    public sealed class PartialEventProcessor<TSequenceBarrierOptions, TSequenceBarrier> : IPartialEventProcessor
        where TSequenceBarrierOptions : ISequenceBarrierOptions
        where TSequenceBarrier : ISequenceBarrier
    {
        private readonly Sequence _sequence = new();
        private readonly SequenceBarrier _sequenceBarrier;
        private TSequenceBarrier _typedSequenceBarrier;

        public PartialEventProcessor(SequenceBarrier sequenceBarrier, TSequenceBarrier typedSequenceBarrier)
        {
            _sequenceBarrier = sequenceBarrier;
            _typedSequenceBarrier = typedSequenceBarrier;
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.AggressiveOptimization)]
        public void ProcessingLoop_Options(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _sequenceBarrier.WaitFor<TSequenceBarrierOptions>(nextSequence);
                if (waitResult.IsTimeout)
                {
                    HandleTimeout(_sequence.Value);
                    return;
                }

                var availableSequence = waitResult.UnsafeAvailableSequence;
                Process(availableSequence);

                _sequence.SetValue(availableSequence);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.AggressiveOptimization)]
        public void ProcessingLoop_Typed(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _typedSequenceBarrier.WaitFor(nextSequence);
                if (waitResult.IsTimeout)
                {
                    HandleTimeout(_sequence.Value);
                    return;
                }

                var availableSequence = waitResult.UnsafeAvailableSequence;
                Process(availableSequence);

                _sequence.SetValue(availableSequence);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void HandleTimeout(long sequence)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void Process(long sequence)
        {
        }
    }

    public interface ISequenceBarrier
    {
        SequenceWaitResult WaitFor(long sequence);
    }

    public sealed class SequenceBarrierClass : ISequenceBarrier
    {
        private readonly ISequencer _sequencer;
        private readonly IWaitStrategy _waitStrategy;
        private readonly DependentSequenceGroup _dependentSequences;
        private CancellationTokenSource _cancellationTokenSource;

        public SequenceBarrierClass(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, Sequence[] dependentSequences)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _dependentSequences = new DependentSequenceGroup(cursorSequence, dependentSequences);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
        public SequenceWaitResult WaitFor(long sequence)
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var availableSequence = _dependentSequences.Value;
            if (availableSequence >= sequence)
            {
                return availableSequence;
            }

            return InvokeWaitStrategy(sequence);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategy(long sequence)
        {
            return _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);
        }
    }

    public struct SequenceBarrierStruct : ISequenceBarrier
    {
        private readonly ISequencer _sequencer;
        private readonly IWaitStrategy _waitStrategy;
        private readonly DependentSequenceGroup _dependentSequences;
        private CancellationTokenSource _cancellationTokenSource;

        public SequenceBarrierStruct(ISequencer sequencer, IWaitStrategy waitStrategy, Sequence cursorSequence, Sequence[] dependentSequences)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _dependentSequences = new DependentSequenceGroup(cursorSequence, dependentSequences);
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | Constants.AggressiveOptimization)]
        public SequenceWaitResult WaitFor(long sequence)
        {
            _cancellationTokenSource.Token.ThrowIfCancellationRequested();

            var availableSequence = _dependentSequences.Value;
            if (availableSequence >= sequence)
            {
                return availableSequence;
            }

            return InvokeWaitStrategy(sequence);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private SequenceWaitResult InvokeWaitStrategy(long sequence)
        {
            return _waitStrategy.WaitFor(sequence, _dependentSequences, _cancellationTokenSource.Token);
        }
    }
}
