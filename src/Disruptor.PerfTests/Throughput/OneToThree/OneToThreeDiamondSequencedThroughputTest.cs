using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Throughput.OneToThree;

/// <summary>
/// Produce an event replicated to two event proces
///           +-----+
///    +----->| EP1 |------+
///    |      +-----+      |
///    |                   v
/// +----+              +-----+
/// | P1 |              | EP3 |
/// +----+              +-----+
///    |                   ^
///    |      +-----+      |
///    +----->| EP2 |------+
///           +-----+
/// Disruptor:
/// ==========
///                    track to prevent wrap
///              +-------------------------------+
///              |                               |
///              |                               v
/// +----+    +====+               +=====+    +----
/// | P1 |---\| RB |/--------------| SB2 |/---| EP3
/// +----+    +====+               +=====+    +----
///      claim   ^  get               |   waitFor
///              |                    |
///           +=====+    +-----+      |
///           | SB1 |/---| EP1 |/-----+
///           +=====+    +-----+      |
///              ^                    |
///              |       +-----+      |
///              +-------| EP2 |/-----+
///             waitFor  +-----+
///
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB1 - SequenceBarrier 1
/// EP1 - EventProcessor 1
/// EP2 - EventProcessor 2
/// SB2 - SequenceBarrier 2
/// EP3 - EventProcessor 3
/// </summary>
public class OneToThreeDiamondSequencedThroughputTest : IThroughputTest
{
    private const int _bufferSize = 1024 * 8;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly long _expectedResult;

    private readonly RingBuffer<FizzBuzzEvent> _ringBuffer = RingBuffer<FizzBuzzEvent>.CreateSingleProducer(FizzBuzzEvent.EventFactory, _bufferSize, new YieldingWaitStrategy());
    private readonly IEventProcessor<FizzBuzzEvent> _eventProcessorFizz;
    private readonly IEventProcessor<FizzBuzzEvent> _eventProcessorBuzz;
    private readonly IEventProcessor<FizzBuzzEvent> _eventProcessorFizzBuzz;
    private readonly FizzBuzzEventHandler _fizzBuzzHandler;

    public OneToThreeDiamondSequencedThroughputTest()
    {
        var sequenceBarrier = _ringBuffer.NewBarrier();

        var fizzHandler = new FizzBuzzEventHandler(FizzBuzzStep.Fizz);
        _eventProcessorFizz = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, fizzHandler);

        var buzzHandler = new FizzBuzzEventHandler(FizzBuzzStep.Buzz);
        _eventProcessorBuzz = EventProcessorFactory.Create(_ringBuffer, sequenceBarrier, buzzHandler);

        var sequenceBarrierFizzBuzz = _ringBuffer.NewBarrier(_eventProcessorFizz.Sequence, _eventProcessorBuzz.Sequence);

        _fizzBuzzHandler = new FizzBuzzEventHandler(FizzBuzzStep.FizzBuzz);
        _eventProcessorFizzBuzz = EventProcessorFactory.Create(_ringBuffer, sequenceBarrierFizzBuzz, _fizzBuzzHandler);

        var temp = 0L;
        for (long i = 0; i < _iterations; i++)
        {
            var fizz = 0 == (i % 3L);
            var buzz = 0 == (i % 5L);

            if (fizz && buzz)
            {
                ++temp;
            }
        }
        _expectedResult = temp;

        _ringBuffer.AddGatingSequences(_eventProcessorFizzBuzz.Sequence);
    }

    public int RequiredProcessorCount => 4;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var latch = new ManualResetEvent(false);
        _fizzBuzzHandler.Reset(latch, _eventProcessorFizzBuzz.Sequence.Value + _iterations);

        var startTask1 = _eventProcessorFizz.Start();
        var startTask2 = _eventProcessorBuzz.Start();
        var startTask3 = _eventProcessorFizzBuzz.Start();
        startTask1.Wait(TimeSpan.FromSeconds(5));
        startTask2.Wait(TimeSpan.FromSeconds(5));
        startTask3.Wait(TimeSpan.FromSeconds(5));

        sessionContext.Start();

        var ringBuffer = _ringBuffer;

        for (long i = 0; i < _iterations; i++)
        {
            var sequence = ringBuffer.Next();
            ringBuffer[sequence].Value = i;
            ringBuffer.Publish(sequence);
        }

        latch.WaitOne();
        sessionContext.Stop();

        var shutdownTask1 = _eventProcessorFizz.Halt();
        var shutdownTask2 = _eventProcessorBuzz.Halt();
        var shutdownTask3 = _eventProcessorFizzBuzz.Halt();
        Task.WaitAll(shutdownTask1, shutdownTask2, shutdownTask3);

        PerfTestUtil.FailIfNot(_expectedResult, _fizzBuzzHandler.FizzBuzzCounter);

        return _iterations;
    }
}
