using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using NUnit.Framework;

namespace Disruptor.PerfTests.Sequencer3P1C
{
    [TestFixture]
    public class Sequencer3P1CDisruptorPerfTest : AbstractSequencer3P1CPerfTest
    {
        private readonly RingBuffer<ValueEvent> _ringBuffer;
        private readonly ValueAdditionEventHandler _eventHandler;
        private readonly ValueProducer[] _valueProducers;
        private readonly Barrier _testStartBarrier = new Barrier(NumProducers);
        private readonly Disruptor<ValueEvent> _disruptor;
        private readonly ManualResetEvent _mru;

        public Sequencer3P1CDisruptorPerfTest()
            : base(20 * Million)
        {
            _disruptor = new Disruptor<ValueEvent>(()=>new ValueEvent(), 
                                                   new MultiThreadedLowContentionClaimStrategy(Size),
                                                   new YieldingWaitStrategy(),
                                                   TaskScheduler.Default);
            _mru = new ManualResetEvent(false);
            _eventHandler = new ValueAdditionEventHandler(Iterations * NumProducers, _mru);
            _disruptor.HandleEventsWith(_eventHandler);
            
            _valueProducers = new ValueProducer[NumProducers];
            _ringBuffer = _disruptor.RingBuffer;

            for (int i = 0; i < NumProducers; i++)
            {
                _valueProducers[i] = new ValueProducer(_testStartBarrier, _ringBuffer, Iterations);
            }
        }

        public override long RunPass()
        {
            _disruptor.Start();

            for (var i = 0; i < NumProducers - 1; i++)
            {
                (new Thread(_valueProducers[i].Run) { Name = "Value producer " + i }).Start();
            }
            
            var sw = Stopwatch.StartNew();
            _valueProducers[NumProducers - 1].Run();

            _mru.WaitOne();

            var opsPerSecond = (NumProducers * Iterations * 1000L) / sw.ElapsedMilliseconds;
            _disruptor.Shutdown();

            return opsPerSecond;
        }

        [Test]
        public override void RunPerformanceTest()
        {
            RunAsUnitTest();
        }
    }
}