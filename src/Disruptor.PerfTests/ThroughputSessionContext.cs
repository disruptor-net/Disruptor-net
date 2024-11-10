using System;
using System.Diagnostics;

namespace Disruptor.PerfTests;

public class ThroughputSessionContext
{
    private readonly Stopwatch _stopwatch = new();

    public double? BatchPercent { get; private set; }
    public double? AverageBatchSize { get; private set; }

    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    public void Reset()
    {
        _stopwatch.Reset();
        BatchPercent = null;
        AverageBatchSize = null;
    }

    public void Start()
    {
        _stopwatch.Start();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void SetBatchData(long batchesProcessedCount, long iterations)
    {
        AverageBatchSize = (double)iterations / batchesProcessedCount;
        BatchPercent = 1 - (double)batchesProcessedCount / iterations;
    }
}
