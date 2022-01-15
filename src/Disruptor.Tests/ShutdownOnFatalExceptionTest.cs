using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Disruptor.Dsl;
using Disruptor.Tests.Support;

namespace Disruptor.Tests;

[TestFixture]
public class ShutdownOnFatalExceptionTest : IDisposable
{
    private readonly Random _random = new();
    private readonly FailingEventHandler _failingEventHandler = new();
    private readonly Disruptor<byte[]> _disruptor;

    public ShutdownOnFatalExceptionTest()
    {
        _disruptor = new Disruptor<byte[]>(() => new byte[256], 1024, TaskScheduler.Current, ProducerType.Single, new BlockingWaitStrategy());
        _disruptor.HandleEventsWith(_failingEventHandler);
        _disruptor.SetDefaultExceptionHandler(new FatalExceptionHandler<byte[]>());
    }

    public void Dispose()
    {
        _disruptor.Shutdown();
    }

    [Test]
    public void ShouldShutdownGracefulEvenWithFatalExceptionHandler()
    {
        var task = Task.Run(() =>
        {
            _disruptor.Start();

            for (var i = 1; i < 10; i++)
            {
                var bytes = new byte[32];
                _random.NextBytes(bytes);
                using (var scope = _disruptor.PublishEvent())
                {
                    bytes.CopyTo(scope.Event(), 0);
                }
            }
        });

        Assert.IsTrue(task.Wait(1000));
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