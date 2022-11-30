using System.Threading;
using Disruptor.Testing.Support;

namespace Disruptor.PerfTests.Support;

public class LongArrayEventHandler : IEventHandler<long[]>
{
    private PaddedLong _value;
    private PaddedLong _batchesProcessed;
    private long _count;
    private ManualResetEvent _signal;

    public long Value => _value.Value;
    public long BatchesProcessed => _batchesProcessed.Value;

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
