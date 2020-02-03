namespace Disruptor.Samples.PullWithBatchedPoller
{
    public class PullWithBatchedPoller
    {
        public static void Main(string[] args)
        {
            var batchSize = 40;
            var ringBuffer = RingBuffer<BatchedPoller<object>.DataEvent>.CreateMultiProducer(() => new BatchedPoller<object>.DataEvent(), 1024);

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
