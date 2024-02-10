using System.Threading;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;

namespace Disruptor.PerfTests.Throughput.OneToOne.ConcurrentQueue;

public class OneToOneQueueThroughputTest_BatchHandler : IThroughputTest, IExternalTest
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 10L;
    private const long _expectedResult = _iterations * 3L;

    private readonly ArrayConcurrentQueue<long> _blockingQueue = new(_bufferSize);
    private readonly PerfAdditionBatchQueueProcessor _queueProcessor;

    public OneToOneQueueThroughputTest_BatchHandler()
    {
        _queueProcessor = new PerfAdditionBatchQueueProcessor(_blockingQueue, _iterations);
    }

    public int RequiredProcessorCount => 2;

    public long Run(ThroughputSessionContext sessionContext)
    {
        var latch = new ManualResetEvent(false);
        _queueProcessor.Reset(latch);
        var future = Task.Run(() => _queueProcessor.Run());
        sessionContext.Start();

        for (long i = 0; i < _iterations; i++)
        {
            _blockingQueue.Enqueue(3L);
        }

        latch.WaitOne();
        sessionContext.Stop();
        _queueProcessor.Halt();
        future.Wait();

        PerfTestUtil.FailIf(_expectedResult, 0);

        return _iterations;
    }
}
