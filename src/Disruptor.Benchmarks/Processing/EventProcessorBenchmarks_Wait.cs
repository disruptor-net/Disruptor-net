using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Benchmarks.Processing;

/// <summary>
/// Use partial copies of the processor types to benchmark the WaitFor invocation.
/// </summary>
public class EventProcessorBenchmarks_Wait
{
    private const int _operationsPerInvoke = 1000;

    private readonly IPartialEventProcessor _processor1;

    public EventProcessorBenchmarks_Wait()
    {
        var waitStrategy = new YieldingWaitStrategy();
        var sequencer = new SingleProducerSequencer(64, waitStrategy);
        var sequence = new Sequence();
        var sequenceBarrier = sequencer.NewBarrier(SequenceWaiterOwner.Unknown, sequence);
        _processor1 = new PartialEventProcessor<EventProcessorHelpers.NoopPublishedSequenceReader>(sequenceBarrier, new EventProcessorHelpers.NoopPublishedSequenceReader());

        sequencer.Publish(42);
        sequence.SetValue(42);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public void ProcessingLoop_Default()
    {
        _processor1.ProcessingLoop(42);
    }

    public interface IPartialEventProcessor
    {
        void ProcessingLoop(long nextSequence);
    }

    /// <summary>
    /// Partial copy of <see cref="EventProcessor{T, TDataProvider, TPublishedSequenceReader, TEventHandler, TOnBatchStartEvaluator, TBatchSizeLimiter}"/>
    /// </summary>
    public sealed class PartialEventProcessor<TPublishedSequenceReader> : IPartialEventProcessor
        where TPublishedSequenceReader : struct, IPublishedSequenceReader
    {
        private readonly Sequence _sequence = new();
        private readonly SequenceBarrier _sequenceBarrier;
        private readonly TPublishedSequenceReader _publishedSequenceReader;

        public PartialEventProcessor(SequenceBarrier sequenceBarrier, TPublishedSequenceReader publishedSequenceReader)
        {
            _sequenceBarrier = sequenceBarrier;
            _publishedSequenceReader = publishedSequenceReader;
        }

        [MethodImpl(MethodImplOptions.NoInlining | Constants.AggressiveOptimization)]
        public void ProcessingLoop(long nextSequence)
        {
            for (var i = 0; i < _operationsPerInvoke; i++)
            {
                var waitResult = _sequenceBarrier.WaitFor(nextSequence);
                if (waitResult.IsTimeout)
                {
                    HandleTimeout(_sequence.Value);
                    return;
                }

                var availableSequence = _publishedSequenceReader.GetHighestPublishedSequence(nextSequence, waitResult.UnsafeAvailableSequence);
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
}
