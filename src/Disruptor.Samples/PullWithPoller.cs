namespace Disruptor.Samples
{
    public class PullWithPoller
    {
        public static void Main(string[] args)
        {
            var ringBuffer = RingBuffer<DataEvent<object>>.CreateMultiProducer(() => new DataEvent<object>(), 1024);
            var poller = ringBuffer.NewPoller();

            var value = GetNextValue(poller);
            
            // Value could be null if no events are available.
            if (null != value)
            {
                // Process value.
            }
        }

        private static object GetNextValue(EventPoller<DataEvent<object>> poller)
        {
            var output = new object[1];
            poller.Poll((ev, sequence, endOfBatch) =>
                        {
                            output[0] = ev.Data;
                            return false;
                        });
            return output[0];
        }

        public class DataEvent<T>
        {
            public T Data { get; set; }

            public T CopyOfData()
            {
                // Copy the data out here.  In this case we have a single reference object, so the pass by
                // reference is sufficient.  But if we were reusing a byte array, then we would need to copy
                // the actual contents.
                return Data;
            }
        }
    }
}