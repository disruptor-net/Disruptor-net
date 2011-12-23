using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Scheduler;
using NUnit.Framework;

namespace Disruptor.PerfTests.UniCast1P1C
{
    [TestFixture]
    public class UniCast1P1CDisruptorWithAffinityPerfTest : AbstractUniCast1P1CPerfTest
    {
        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly ValueAdditionEventHandler _eventHandler;
        private readonly Dsl.Disruptor<ValueEvent> _disruptor;
        private readonly ManualResetEvent _mru;
        private readonly RoundRobinThreadAffinedTaskScheduler _scheduler;

        public UniCast1P1CDisruptorWithAffinityPerfTest()
            : base(100 * Million)
        {
            _scheduler = new RoundRobinThreadAffinedTaskScheduler(2);
            _disruptor = new Dsl.Disruptor<ValueEvent>(() => new ValueEvent(),
                                                          new SingleThreadedClaimStrategy(BufferSize),
                                                          new YieldingWaitStrategy(),
                                                          _scheduler);

            _mru = new ManualResetEvent(false);
            _eventHandler = new ValueAdditionEventHandler(Iterations, _mru);
            _disruptor.HandleEventsWith(_eventHandler);
            _ringBuffer = _disruptor.RingBuffer;
        }

        public override long RunPass()
        {
            _disruptor.Start();

            var sw = Stopwatch.StartNew();
            Task.Factory.StartNew(
                () =>
                {
                    for (long i = 0; i < Iterations; i++)
                    {
                        long sequence = _ringBuffer.Next();
                        _ringBuffer[sequence].Value = i;
                        _ringBuffer.Publish(sequence);
                    }
                }, CancellationToken.None, TaskCreationOptions.None, _scheduler);

            _mru.WaitOne();

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;
            _disruptor.Shutdown();
            _scheduler.Dispose();

            Assert.AreEqual(ExpectedResult, _eventHandler.Value, "RunDisruptorPass");

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}