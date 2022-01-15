using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

/// <summary>
/// Use partial copies of the processor types to benchmark the WaitFor invocation.
/// </summary>
public class EventProcessorBenchmarks_Wait
{
    private const int _operationsPerInvoke = 1000;

    private readonly IPartialEventProcessor _processor1;
    private readonly PartialEventProcessor<ValueSequenceBarrier, TimeoutDeactivated> _processor2;

    public EventProcessorBenchmarks_Wait()
    {
        var sequencer = new SingleProducerSequencer(64, new YieldingWaitStrategy());
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequencer.NewBarrier());
        var eventProcessorType = typeof(PartialEventProcessor<,>).MakeGenericType(sequenceBarrierProxy.GetType(), typeof(TimeoutDeactivated));
        _processor1 = (IPartialEventProcessor)Activator.CreateInstance(eventProcessorType, sequenceBarrierProxy, new TimeoutDeactivated());

        var cursorSequence = new Sequence();
        _processor2 = new PartialEventProcessor<ValueSequenceBarrier, TimeoutDeactivated>(new ValueSequenceBarrier(sequencer, new YieldingWaitStrategy(), cursorSequence), default);

        sequencer.Publish(42);
        cursorSequence.SetValue(42);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_CheckTimeout()
    {
        _processor1.ProcessingLoop_CheckTimeout(42);
    }

    // [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_CheckTimeoutAndCustomSequenceBarrier()
    {
        _processor2.ProcessingLoop_CheckTimeout(42);
    }

    // [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_TimeoutActivationStruct()
    {
        _processor1.ProcessingLoop_TimeoutActivationStruct(42);
    }

    // [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_TimeoutActivationStructAndCustomSequenceBarrier()
    {
        _processor2.ProcessingLoop_TimeoutActivationStruct(42);
    }

    // [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_IgnoreTimeout()
    {
        _processor1.ProcessingLoop_IgnoreTimeout(42);
    }

    public interface IPartialEventProcessor
    {
        void ProcessingLoop_CheckTimeout(long nextSequence);
        void ProcessingLoop_TimeoutActivationStruct(long nextSequence);
        void ProcessingLoop_IgnoreTimeout(long nextSequence);
    }

    /// <summary>
    /// Partial copy of <see cref="EventProcessor{T, TDataProvider, TSequenceBarrier, TEventHandler, TBatchStartAware}"/>
    /// </summary>
    public sealed class PartialEventProcessor<TSequenceBarrier, TTimeoutActivation> : IPartialEventProcessor
        where TSequenceBarrier : ISequenceBarrier
        where TTimeoutActivation : ITimeoutActivation
    {
        private readonly Sequence _sequence = new();
        private TSequenceBarrier _sequenceBarrier;
        private TTimeoutActivation _timeoutActivation;

        public PartialEventProcessor(TSequenceBarrier sequenceBarrier, TTimeoutActivation timeoutActivation)
        {
            _sequenceBarrier = sequenceBarrier;
            _timeoutActivation = timeoutActivation;
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.AggressiveOptimization)]
        public void ProcessingLoop_CheckTimeout(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _sequenceBarrier.WaitFor(nextSequence);
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
        public void ProcessingLoop_TimeoutActivationStruct(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _sequenceBarrier.WaitFor(nextSequence);
                if (_timeoutActivation.Enabled)
                {
                    if (waitResult.IsTimeout)
                    {
                        HandleTimeout(_sequence.Value);
                        return;
                    }
                }

                var availableSequence = waitResult.UnsafeAvailableSequence;
                Process(availableSequence);

                _sequence.SetValue(availableSequence);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.AggressiveOptimization)]
        public void ProcessingLoop_IgnoreTimeout(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _sequenceBarrier.WaitFor(nextSequence);

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

    public interface ITimeoutActivation
    {
        bool Enabled { get; }
    }

    public struct TimeoutActivated : ITimeoutActivation
    {
        public bool Enabled => true;
    }

    public struct TimeoutDeactivated : ITimeoutActivation
    {
        public bool Enabled => false;
    }

    public struct ValueSequenceBarrier : ISequenceBarrier
    {
        private readonly SingleProducerSequencer _sequencer;
        private readonly YieldingWaitStrategy _waitStrategy;
        private readonly Sequence _cursorSequence;
        private readonly ISequence _dependentSequence;
        private volatile CancellationTokenSource _cancellationTokenSource;

        public ValueSequenceBarrier(SingleProducerSequencer sequencer, YieldingWaitStrategy waitStrategy, Sequence cursorSequence)
        {
            _sequencer = sequencer;
            _waitStrategy = waitStrategy;
            _cursorSequence = cursorSequence;
            _dependentSequence = SequenceGroups.CreateReadOnlySequence(cursorSequence, Array.Empty<ISequence>());
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SequenceWaitResult WaitFor(long sequence)
        {
            var cancellationToken = _cancellationTokenSource.Token;
            cancellationToken.ThrowIfCancellationRequested();

            var result = _waitStrategy.WaitFor(sequence, _cursorSequence, _dependentSequence, cancellationToken);

            if (result.UnsafeAvailableSequence < sequence)
                return result;

            return _sequencer.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence);
        }

        public long Cursor => default;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void ResetProcessing()
        {
        }

        public void CancelProcessing()
        {
        }
    }
}