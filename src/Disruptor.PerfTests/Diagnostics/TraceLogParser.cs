using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Diagnostics.Windows.Tracing;
using BenchmarkDotNet.Engines;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;

namespace Disruptor.PerfTests.Diagnostics
{
    public class TraceLogParser
    {
        private readonly List<(double timeStamp, ulong instructionPointer, int profileSource)> samples = new List<(double timeStamp, ulong instructionPointer, int profileSource)>();
        private readonly Dictionary<int, int> profileSourceIdToInterval = new Dictionary<int, int>();

        public static IEnumerable<PerfMetric> Parse(string etlFilePath, PerfCounterSource[] counters)
        {
            var etlxFilePath = TraceLog.CreateFromEventTraceLogFile(etlFilePath);

            try
            {
                using (var traceLog = new TraceLog(etlxFilePath))
                {
                    var traceLogEventSource = traceLog.Events.GetSource();

                    return new TraceLogParser().Parse(traceLogEventSource, counters);
                }
            }
            finally
            {
                File.Delete(etlxFilePath);
            }
        }

        private IEnumerable<PerfMetric> Parse(TraceLogEventSource traceLogEventSource, PerfCounterSource[] counters)
        {
            var kernelEventsParser = new KernelTraceEventParser(traceLogEventSource);

            kernelEventsParser.PerfInfoCollectionStart += OnPmcIntervalChange;
            kernelEventsParser.PerfInfoPMCSample += OnPmcEvent;

            traceLogEventSource.Process();

            return CalculateMetrics(counters);
        }

        private IEnumerable<PerfMetric> CalculateMetrics(PerfCounterSource[] counters)
        {
            var profileSourceIdToCounter = counters.ToDictionary(counter => counter.ProfileSourceId);
            // var ProfileSourceIdToCount = new Dictionary<int, ulong>();

            foreach (var sample in samples)
            {
                // var interval = profileSourceIdToInterval[sample.profileSource];

                profileSourceIdToCounter[sample.profileSource].OnSample(sample.instructionPointer);

                // ProfileSourceIdToCount.TryGetValue(sample.profileSource, out ulong existing);
                // ProfileSourceIdToCount[sample.profileSource] = existing + (ulong)interval;
            }

            return profileSourceIdToCounter.Values.Select(x => new PerfMetric(x.Counter, x.Count));
        }

        private void OnPmcIntervalChange(SampledProfileIntervalTraceData data)
        {
            if (profileSourceIdToInterval.TryGetValue(data.SampleSource, out int storedInterval) && storedInterval != data.NewInterval)
                throw new NotSupportedException("Sampling interval change is not supported!");

            profileSourceIdToInterval[data.SampleSource] = data.NewInterval;
        }

        private void OnPmcEvent(PMCCounterProfTraceData data)
        {
            if (data.ProcessID != Environment.ProcessId)
                return;

            samples.Add((data.TimeStampRelativeMSec, data.InstructionPointer, data.ProfileSource));
        }
    }
}
