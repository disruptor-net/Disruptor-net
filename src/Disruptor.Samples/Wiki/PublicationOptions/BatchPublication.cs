using System;
using Disruptor.Samples.Wiki.GettingStarted;

namespace Disruptor.Samples.Wiki.PublicationOptions
{
    public class BatchPublication
    {
        private readonly RingBuffer<SampleEvent> _ringBuffer = new RingBuffer<SampleEvent>(() => new SampleEvent(), 1024);

        public void PublishEventBatchUsingScope(ReadOnlySpan<int> ids, double value)
        {
            using (var scope = _ringBuffer.PublishEvents(ids.Length))
            {
                for (var index = 0; index < ids.Length; index++)
                {
                    var sampleEvent = scope.Event(index);
                    sampleEvent.Id = ids[index];
                    sampleEvent.Value = value;
                }
            }
        }

        public void PublishEventBatchUsingRawApi(ReadOnlySpan<int> ids, double value)
        {
            var hi = _ringBuffer.Next(ids.Length);
            var lo = hi - (ids.Length - 1);
            try
            {
                for (var index = 0; index < ids.Length; index++)
                {
                    var sampleEvent = _ringBuffer[lo + index];
                    sampleEvent.Id = ids[index];
                    sampleEvent.Value = value;
                }
            }
            finally
            {
                _ringBuffer.Publish(lo, hi);
            }
        }
    }
}
