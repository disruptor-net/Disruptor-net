using System.Threading;
using Disruptor.Testing.Support;

namespace Disruptor.PerfTests.Support;

public class LongArrayEventHandler(int? cpu = null) : IEventHandler<long[]>
{
    private PaddedLong _value;
    private PaddedLong _batchesProcessed;
    private long _count;
    private ManualResetEvent _signal;
    private ThreadAffinityScope _affinityScope;

    public long Value => _value.Value;
    public long BatchesProcessed => _batchesProcessed.Value;

    public void OnStart()
    {
        _affinityScope = ThreadAffinityUtil.SetThreadAffinity(cpu, ThreadPriority.Highest);
    }

    public void OnShutdown()
    {
        _affinityScope.Dispose();
    }

    public void Reset(ManualResetEvent signal, long expectedCount)
    {
        _value.Value = 0;
        _signal = signal;
        _count = expectedCount;
        _batchesProcessed.Value = 0;
    }

    public void OnEvent(long[] value, long sequence, bool endOfBatch)
    {
        for (var i = 0; i < value.Length; i++)
        {
            _value.Value = _value.Value + value[i];
        }

        if (--_count == 0)
        {
            _signal?.Set();
        }
    }

    public void OnBatchStart(long batchSize)
    {
        _batchesProcessed.Value = _batchesProcessed.Value + 1;
    }
}
