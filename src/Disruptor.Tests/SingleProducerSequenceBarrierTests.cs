namespace Disruptor.Tests;

public class SingleProducerSequenceBarrierTests : SequenceBarrierTests
{
    public SingleProducerSequenceBarrierTests()
        : base(new SingleProducerSequencer(64, new BlockingWaitStrategy()))
    {
    }
}
