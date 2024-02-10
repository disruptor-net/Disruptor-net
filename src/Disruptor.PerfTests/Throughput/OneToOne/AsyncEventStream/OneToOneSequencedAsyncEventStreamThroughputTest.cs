using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Throughput.OneToOne.AsyncEventStream;

public class OneToOneSequencedAsyncEventStreamThroughputTest : IThroughputTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    private readonly RingBuffer<PerfEvent> _ringBuffer;
    private readonly StreamProcessor _streamProcessor;
    private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);

    public OneToOneSequencedAsyncEventStreamThroughputTest()
    {
        _ringBuffer = RingBuffer<PerfEvent>.CreateSingleProducer(PerfEvent.EventFactory, _bufferSize, new AsyncWaitStrategy());
        var asyncEventStream = _ringBuffer.NewAsyncEventStream();
        _streamProcessor = new StreamProcessor(asyncEventStream);
    }

    public int RequiredProcessorCount => 2;

    ///////////////////////////////////////////////////////////////////////////////////////////////

    public class StreamProcessor
    {
        private readonly AsyncEventStream<PerfEvent> _stream;
        private CancellationTokenSource _cancellationTokenSource;
        private long _count;
        private ManualResetEvent _completedSignal;

        public StreamProcessor(AsyncEventStream<PerfEvent> stream)
        {
            _stream = stream;
        }

        public long Value { get; private set; }
        public long BatchesProcessedCount { get; private set; }

        public async Task Run()
        {
            try
            {
                await foreach (var batch in _stream.WithCancellation(_cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    foreach (var data in batch)
                    {
                        Value += data.Value;

                        if (++_count == _iterations)
                            _completedSignal.Set();
                    }

                    BatchesProcessedCount++;
                }
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void Halt() => _cancellationTokenSource.Cancel();

        public void Reset(ManualResetEvent completedSignal)
        {
            _stream.ResetNextEnumeratorSequence();
            _completedSignal = completedSignal;
            _count = 0;
            _cancellationTokenSource = new();
            Value = 0L;
            BatchesProcessedCount = 0;
        }

        public Task Start()
        {
            return Task.Run(async() => await Run());
        }
    }

    public long Run(ThroughputSessionContext sessionContext)
    {
        var completedSignal = new ManualResetEvent(false);
        _streamProcessor.Reset(completedSignal);

        var processorTask = _streamProcessor.Start();
        sessionContext.Start();

        var ringBuffer = _ringBuffer;
        for (var i = 0; i < _iterations; i++)
        {
            var next = ringBuffer.Next();
            ringBuffer[next].Value = i;
            ringBuffer.Publish(next);
        }

        completedSignal.WaitOne();

        sessionContext.Stop();

        _streamProcessor.Halt();
        processorTask.Wait();

        sessionContext.SetBatchData(_streamProcessor.BatchesProcessedCount, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _streamProcessor.Value, $"Poll runnable should have processed {_expectedResult} but was {_streamProcessor.Value}");

        return _iterations;
    }
}
