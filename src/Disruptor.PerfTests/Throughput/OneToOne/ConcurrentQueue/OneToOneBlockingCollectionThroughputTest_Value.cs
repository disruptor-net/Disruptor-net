using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Throughput.OneToOne.ConcurrentQueue;

public class OneToOneBlockingCollectionThroughputTest_Value : IThroughputTest, IExternalTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 10L;

    private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

    private readonly BlockingCollection<PerfValueEvent> _queue;
    private readonly AdditionEventHandler _eventHandler;
    private readonly EventProcessor _eventProcessor;

    public OneToOneBlockingCollectionThroughputTest_Value()
    {
        _queue = new BlockingCollection<PerfValueEvent>(_bufferSize);
        _eventHandler = new AdditionEventHandler();
        _eventProcessor = new EventProcessor(_queue, _eventHandler);
    }

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        _eventHandler.Reset(_iterations - 1);
        _eventProcessor.Start();

        sessionContext.Start();

        for (long i = 0; i < _iterations; i++)
        {
            var data = new PerfValueEvent { Value = i };
            _queue.Add(data);
        }

        _eventHandler.WaitForSequence();
        sessionContext.Stop();
        _eventProcessor.Stop();

        sessionContext.SetBatchData(_eventHandler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

        return _iterations;
    }

    private class EventProcessor
    {
        private readonly BlockingCollection<PerfValueEvent> _queue;
        private readonly AdditionEventHandler _eventHandler;
        private Task _task;
        private CancellationTokenSource _cancellationTokenSource;

        public EventProcessor(BlockingCollection<PerfValueEvent> queue, AdditionEventHandler eventHandler)
        {
            _queue = queue;
            _eventHandler = eventHandler;
        }

        public void Start()
        {
            var started = new ManualResetEventSlim();

            _cancellationTokenSource = new CancellationTokenSource();
            _task = Task.Run(() =>
            {
                started.Set();

                try
                {
                    foreach (var perfEvent in _queue.GetConsumingEnumerable(_cancellationTokenSource.Token))
                    {
                        _eventHandler.OnBatchStart(1);

                        var localPerfEvent = perfEvent;
                        _eventHandler.OnEvent(ref localPerfEvent, perfEvent.Value, true);
                    }
                }
                catch (OperationCanceledException)
                {
                }
            });

            started.Wait();
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _task.Wait();
        }
    }
}
