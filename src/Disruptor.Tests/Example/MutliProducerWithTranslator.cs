using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Tests.Example
{
    public class MutliProducerWithTranslator
    {
        public const int _ringSize = 1024;

        public static void Main(string[] args)
        {
            var disruptor = new Disruptor<ObjectBox>(() => new ObjectBox(), _ringSize, TaskScheduler.Current, ProducerType.Multi, new BlockingWaitStrategy());
            disruptor.HandleEventsWith(new Consumer()).Then(new Consumer());
            var ringBuffer = disruptor.RingBuffer;
            var message = new Message();
            var transportable = new Transportable();
            var streamName = "com.lmax.wibble";
            Console.WriteLine($"publishing {_ringSize} messages");
            var p = new Publisher();
            for (var i = 0; i < _ringSize; i++)
            {
                ringBuffer.PublishEvent(p, message, transportable, streamName);
                Thread.Sleep(10);
            }
            Console.WriteLine("start disruptor");
            disruptor.Start();
            Console.WriteLine("continue publishing disruptor");
            while (true)
            {
                ringBuffer.PublishEvent(p, message, transportable, streamName);
                Thread.Sleep(10);
            }
        }

        public class Message
        {
        }

        public class Transportable
        {
        }

        public class ObjectBox
        {
            private Message _message;
            private Transportable _transportable;
            private string _string;

            public void SetMessage(Message arg0)
            {
                _message = arg0;
            }

            public void SetTransportable(Transportable arg1)
            {
                _transportable = arg1;
            }

            public void SetStreamName(string arg2)
            {
                _string = arg2;
            }
        }

        public class Publisher : IEventTranslatorThreeArg<ObjectBox, Message, Transportable, string>
        {
            public void TranslateTo(ObjectBox @event, long sequence, Message arg0, Transportable arg1, string arg2)
            {
                @event.SetMessage(arg0);
                @event.SetTransportable(arg1);
                @event.SetStreamName(arg2);
            }
        }

        public class Consumer : IEventHandler<ObjectBox>
        {
            public void OnEvent(ObjectBox data, long sequence, bool endOfBatch)
            {
            }
        }
    }
}