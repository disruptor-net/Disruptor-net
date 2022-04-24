namespace Disruptor.Tests;

public class MultiProducerSequenceBarrierTests : SequenceBarrierTests
{
    public MultiProducerSequenceBarrierTests()
        : base(new MultiProducerSequencer(64, new BlockingWaitStrategy()))
    {
    }
}
