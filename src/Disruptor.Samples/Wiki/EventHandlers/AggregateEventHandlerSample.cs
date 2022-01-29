using System.Threading;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki.EventHandlers;

public class AggregateEventHandlerSample
{
    public static void Run(bool useAggregation)
    {
        var disruptor = new Disruptor<Event>(() => new Event(), 1024);

        if (useAggregation)
        {
            disruptor.HandleEventsWith(new AggregateEventHandler<Event>(new Handler1(), new Handler2()))
                     .Then(new Handler3());
        }
        else
        {
            disruptor.HandleEventsWith(new Handler1())
                     .Then(new Handler2())
                     .Then(new Handler3());
        }
    }

    public class Event
    {
    }

    public class Handler1 : IEventHandler<Event>
    {
        public void OnStart()
        {
            Thread.CurrentThread.Name = "Handler1 main loop";
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
        }

        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler2 : IEventHandler<Event>
    {
        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
        }
    }

    public class Handler3 : IEventHandler<Event>
    {
        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
        }
    }
}
