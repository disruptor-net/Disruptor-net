using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Processing;

namespace Disruptor.Samples;

public class DynamicallyAddHandler
{
    public static void Main(string[] args)
    {
        var disruptor = new Disruptor<DynamicEvent>(() => new DynamicEvent(), 1024, TaskScheduler.Current);
        var ringBuffer = disruptor.Start();

        // Construct 2 batch event processors.
        var handler1 = new DynamicHandler();
        var processor1 = EventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(handler1), handler1);

        var handler2 = new DynamicHandler();
        var processor2 = EventProcessorFactory.Create(ringBuffer, ringBuffer.NewBarrier(handler2, processor1.Sequence), handler2);

        // Dynamically add both sequences to the ring buffer
        ringBuffer.AddGatingSequences(processor1.Sequence, processor2.Sequence);

        // Start the new batch processors.
        var t1 = processor1.Start();
        var t2 = processor2.Start();

        Thread.Sleep(5000);

        // Remove a processor.

        // Stop the processor
        processor2.Halt();

        // Wait for shutdown the complete
        handler2.WaitShutdown(); // Or simply t2.Wait()

        // Remove the gating sequence from the ring buffer
        ringBuffer.RemoveGatingSequence(processor2.Sequence);
    }

    public class DynamicEvent
    {
    }

    public class DynamicHandler : IEventHandler<DynamicEvent>
    {
        private readonly ManualResetEvent _shutdownSignal = new(false);

        public void OnEvent(DynamicEvent data, long sequence, bool endOfBatch)
        {
        }

        public void OnStart()
        {
        }

        public void OnShutdown()
        {
            _shutdownSignal.Set();
        }

        public void WaitShutdown()
        {
            _shutdownSignal.WaitOne();
        }
    }
}
