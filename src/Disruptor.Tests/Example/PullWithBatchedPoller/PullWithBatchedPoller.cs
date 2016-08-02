using System;

namespace Disruptor.Tests.Example.PullWithBatchedPoller
{
    public class PullWithBatchedPoller
    {
        public static void Main(string[] args)
        {
            var batchSize = 40;
            var ringBuffer = RingBuffer<BatchedPoller<object>.DataEvent<object>>.CreateMultiProducer(() => new BatchedPoller<object>.DataEvent<object>(), 1024);

            var poller = new BatchedPoller<object>(ringBuffer, batchSize);

            var value = poller.Poll();

            // Value could be null if no events are available.
            if (null != value)
            {
                // Process value.
            }
        }
    }
}