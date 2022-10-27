using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;

namespace Disruptor.PerfTests.Diagnostics;

public class PerfCounterSession : IDisposable
{
    private static readonly PerfCounter[] _counters = new[]
    {
        new PerfCounter("BranchInstructions", null),
    };

    private TraceEventSession _traceEventSession;
    private PerfCounterSource[] _counterSources;

    public void Start()
    {
        _counterSources = LoadCounterSources();

        var profileSourceIds = _counterSources.Select(x => x.ProfileSourceId).ToArray();
        var profileSourceIntervals = _counterSources.Select(x => x.Interval).ToArray();
        TraceEventProfileSources.Set(profileSourceIds, profileSourceIntervals);

        _traceEventSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName, "Foo.etl")
        {
            BufferSizeMB = 256,
            CpuSampleIntervalMSec = 1F,
            StackCompression = true,
        };

        var keywords = KernelTraceEventParser.Keywords.Profile | KernelTraceEventParser.Keywords.PMCProfile;
        var stackCapture = KernelTraceEventParser.Keywords.Profile;
        _traceEventSession.EnableKernelProvider(keywords, stackCapture);
    }

    private PerfCounterSource[] LoadCounterSources()
    {
        var profileSourceInfos = TraceEventProfileSources.GetInfo();

        return _counters.Select(x => new PerfCounterSource(x, profileSourceInfos[x.Name])).ToArray();
    }

    public void Stop()
    {
        _traceEventSession.Stop();

        var metrics = TraceLogParser.Parse(_traceEventSession.FileName, _counterSources);

        foreach (var metric in metrics)
        {
            Console.WriteLine($"{metric.Counter.Name} => {metric.Value}");
        }
    }

    public void Dispose()
    {
        _traceEventSession?.Dispose();
    }

    // intervals
    // { HardwareCounter.InstructionRetired, _ => 1_000_000 },
    // { HardwareCounter.BranchMispredictions, _ => 1_000 },
    // { HardwareCounter.CacheMisses, _ => 1_000 }
}
