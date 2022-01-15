using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples;

public class ThreeToOneDisruptor
{
    public static void Main(string[] args)
    {
        var disruptor = new Disruptor<DataEvent>(() => new DataEvent(3), 1024, TaskScheduler.Default);
        var handler1 = new TransformingHandler(0);
        var handler2 = new TransformingHandler(1);
        var handler3 = new TransformingHandler(2);
        var collator = new CollatingHandler();

        disruptor.HandleEventsWith(handler1, handler2, handler3).Then(collator);

        disruptor.Start();
    }

    public class DataEvent
    {
        public DataEvent(int size)
        {
            Output = new object[size];
        }

        public object Input { get; set; }
        public object[] Output { get; set; }
    }

    public class TransformingHandler : IEventHandler<DataEvent>
    {
        private readonly int _outputIndex;

        public TransformingHandler(int outputIndex)
        {
            _outputIndex = outputIndex;
        }

        public void OnEvent(DataEvent data, long sequence, bool endOfBatch)
        {
            data.Output[_outputIndex] = DoSomething(data.Input);
        }

        private object DoSomething(object input) => input;
    }

    public class CollatingHandler : IEventHandler<DataEvent>
    {
        public void OnEvent(DataEvent data, long sequence, bool endOfBatch)
        {
            Collate(data.Output);
        }

        private void Collate(object[] output)
        {
            // Do required collation here...
        }
    }
}