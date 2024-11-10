using System;
using System.Diagnostics;
using HdrHistogram;

namespace Disruptor.PerfTests;

public class LatencySessionContext
{
    private readonly Stopwatch _stopwatch = new();
    public LongHistogram Histogram { get; } = new(10000000000L, 4);
    public TimeSpan ElapsedTime => _stopwatch.Elapsed;

    public void Reset()
    {
        Histogram.Reset();
        _stopwatch.Reset();
    }

    public void Start()
    {
        _stopwatch.Start();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }
}
