using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using Disruptor.Dsl;

namespace Disruptor.Tests.Example
{
    public class DynamiclyAddHandler
    {
        public static void Main(string[] args)
        {
            var executor = new BasicExecutor(TaskScheduler.Current);
            var disruptor = new Disruptor<StubEvent>(() => new StubEvent(-1), 1024, TaskScheduler.Current);
            var ringBuffer = disruptor.Start();

            // Construct 2 batch event processors.
            var handler1 = new DynamicHandler();
            var processor1 = new BatchEventProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(), handler1);

            var handler2 = new DynamicHandler();
            var processor2 = new BatchEventProcessor<StubEvent>(ringBuffer, ringBuffer.NewBarrier(processor1.Sequence), handler2);

            // Dynamically add both sequences to the ring buffer
            ringBuffer.AddGatingSequences(processor1.Sequence, processor2.Sequence);

            // Start the new batch processors.
            executor.Execute(processor1.Run);
            executor.Execute(processor2.Run);
            
            // Remove a processor.

            // Stop the processor
            processor2.Halt();
            // Wait for shutdown the complete
            handler2.WaitShutdown();
            // Remove the gating sequence from the ring buffer
            ringBuffer.RemoveGatingSequence(processor2.Sequence);
        }

        private class DynamicHandler : IEventHandler<StubEvent>, ILifecycleAware
        {
            private readonly ManualResetEvent _shutdownSignal = new ManualResetEvent(false);

            public void OnEvent(StubEvent data, long sequence, bool endOfBatch)
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
}