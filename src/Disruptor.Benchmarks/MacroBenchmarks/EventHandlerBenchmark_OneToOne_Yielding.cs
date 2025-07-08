using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using Disruptor.Benchmarks.Util;
using Disruptor.Processing;
using Disruptor.Testing.Support;

namespace Disruptor.Benchmarks.MacroBenchmarks;

// [HardwareCounters(HardwareCounter.BranchMispredictions, HardwareCounter.BranchInstructions, HardwareCounter.CacheMisses, HardwareCounter.LlcMisses)]
[ThroughputColumn]
public class EventHandlerBenchmark_OneToOne_Yielding
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly RingBuffer<Event> _ringBuffer;
    private readonly Handler _eventHandler;
    private readonly IEventProcessor<Event> _eventProcessor;
    private long _expectedCount;

    public EventHandlerBenchmark_OneToOne_Yielding()
    {
        _eventHandler = new Handler();
        _ringBuffer = new RingBuffer<Event>(() => new Event(), new SingleProducerSequencer(_bufferSize, new YieldingWaitStrategy()));
        var sequenceBarrier = _ringBuffer.NewBarrier();
        _eventProcessor = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, _eventHandler);
        _ringBuffer.AddGatingSequences(_eventProcessor.Sequence);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _expectedCount = _eventProcessor.Sequence.Value + _iterations;

        _eventHandler.Reset(_expectedCount);
        var startTask = _eventProcessor.Start();

        startTask.Wait(TimeSpan.FromSeconds(5));
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        var shutdownTask = _eventProcessor.Halt();
        shutdownTask.Wait();
    }

    [Benchmark(OperationsPerInvoke = (int)_iterations)]
    public long Run()
    {
        var ringBuffer = _ringBuffer;

        for (long i = 0; i < _iterations; i++)
        {
            var s = ringBuffer.Next();
            ringBuffer[s].Value = i;
            ringBuffer.Publish(s);
        }

        _eventHandler.WaitForSequence();

        return _iterations;
    }

    public class Event
    {
        public long Value;
    }

    public class Handler : IEventHandler<Event>
    {
        private PaddedLong _value;
        private long _latchSequence;
        private readonly ManualResetEvent _latch = new(false);

        public long Value => _value.Value;

        public void WaitForSequence()
        {
            _latch.WaitOne();
        }

        public void Reset(long expectedSequence)
        {
            _value.Value = 0;
            _latch.Reset();
            _latchSequence = expectedSequence;
        }

        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
            _value.Value += data.Value;

            if(_latchSequence == sequence)
            {
                _latch.Set();
            }
        }
    }
}
