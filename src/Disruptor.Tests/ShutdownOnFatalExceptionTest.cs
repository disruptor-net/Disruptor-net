using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Disruptor.Dsl;

namespace Disruptor.Tests
{
    [TestFixture]
    public class ShutdownOnFatalExceptionTest
    {
        private readonly Random _random = new Random();
        private readonly FailingEventHandler _failingEventHandler = new FailingEventHandler();
        private Disruptor<byte[]> _disruptor;

        [SetUp]
        public void SetUp()
        {
            _disruptor = new Disruptor<byte[]>(() => new byte[256], 1024, TaskScheduler.Current, ProducerType.Single, new BlockingWaitStrategy());
            _disruptor.HandleEventsWith(_failingEventHandler);
            _disruptor.SetDefaultExceptionHandler(new FatalExceptionHandler());
        }

        [Test, Timeout(1000)]
        public void ShouldShutdownGracefulEvenWithFatalExceptionHandler()
        {
            _disruptor.Start();

            for (var i = 1; i < 10; i++)
            {
                var bytes = new byte[32];
                _random.NextBytes(bytes);
                _disruptor.PublishEvent(new ByteArrayTranslator(bytes));
            }
        }

        public class ByteArrayTranslator : IEventTranslator<byte[]>
        {
            private readonly byte[] _bytes;

            public ByteArrayTranslator(byte[] bytes)
            {
                _bytes = bytes;
            }

            public void TranslateTo(byte[] eventData, long sequence)
            {
                _bytes.CopyTo(eventData, 0);
            }
        }

        [TearDown]
        public void Teardown()
        {
            _disruptor.Shutdown();
        }


        private class FailingEventHandler : IEventHandler<byte[]>
        {
            private int _count = 0;

            public void OnEvent(byte[] data, long sequence, bool endOfBatch)
            {
                _count++;
                if (_count == 3)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}
