using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Scheduler;
using NUnit.Framework;

namespace Disruptor.PerfTests.MultiCast1P3C
{
    [TestFixture]
    public class MultiCast1P3CDisruptorWithAffinityPerfTest:AbstractMultiCast1P3CPerfTest
    {
        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly ValueMutationEventHandler _handler1;
        private readonly ValueMutationEventHandler _handler2;
        private readonly ValueMutationEventHandler _handler3;
        private readonly CountdownEvent _latch;
        private readonly Dsl.Disruptor<ValueEvent> _disruptor;
        private readonly RoundRobinThreadAffinedTaskScheduler _scheduler;

        public MultiCast1P3CDisruptorWithAffinityPerfTest()
            : base(100 * Million)
        {
            _scheduler = new RoundRobinThreadAffinedTaskScheduler(4);
            _disruptor = new Dsl.Disruptor<ValueEvent>(() => new ValueEvent(),
                                                      new SingleThreadedClaimStrategy(Size),
                                                      new YieldingWaitStrategy(), 
                                                      _scheduler);

            _latch = new CountdownEvent(3);

            _handler1 = new ValueMutationEventHandler(Operation.Addition, Iterations, _latch);
            _handler2 = new ValueMutationEventHandler(Operation.Substraction, Iterations, _latch);
            _handler3 = new ValueMutationEventHandler(Operation.And, Iterations, _latch);

            _disruptor.HandleEventsWith(_handler1, _handler2, _handler3);
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
                            var sequence = _ringBuffer.Next();
                            _ringBuffer[sequence].Value = i;
                            _ringBuffer.Publish(sequence);
                        }
                    }, CancellationToken.None, TaskCreationOptions.None, _scheduler);
            
            _latch.Wait();

            var opsPerSecond = (Iterations * 1000L) / sw.ElapsedMilliseconds;

            _disruptor.Shutdown();
            _scheduler.Dispose();

            Assert.AreEqual(ExpectedResults[0], _handler1.Value, "Addition");
            Assert.AreEqual(ExpectedResults[1], _handler2.Value, "Sub");
            Assert.AreEqual(ExpectedResults[2], _handler3.Value, "And");

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}