using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.GettingStarted;

public class SampleConstruct
{
    public void Main()
    {
        // Construct the Disruptor with a SingleProducerSequencer
        // var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), 1024, TaskScheduler.Default, ProducerType.Single, new BlockingWaitStrategy());

        // Create a disruptor for single producer usage.
        var producerType = ProducerType.Single;
        var taskScheduler = TaskScheduler.Default;
        var waitStrategy = new BlockingWaitStrategy();
        var factory = () => new SampleEvent();

        var disruptor = new Disruptor<SampleEvent>(factory, 1024, taskScheduler, producerType, waitStrategy);

        // Configure a simple chain of consumers.
        // Handler1 will process events first and then Handler2.
        disruptor.HandleEventsWith(new Handler1()).Then(new Handler2());

        // Configure a graph of consumers.
        // Handler1 will process events first, and then Handler2A and Handler2B (in parallel) and finally Handler3.
        var step1 = disruptor.HandleEventsWith(new Handler1());
        var step2A = step1.Then(new Handler2A());
        var step2B = step1.Then(new Handler2B());
        step2A.And(step2B).Then(new Handler3());
    }

    public class Handler1 : IEventHandler<SampleEvent>
    {
        public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler2 : IEventHandler<SampleEvent>
    {
        public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler2A : IEventHandler<SampleEvent>
    {
        public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler2B : IEventHandler<SampleEvent>
    {
        public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler3 : IEventHandler<SampleEvent>
    {
        public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
        {
        }
    }
}
