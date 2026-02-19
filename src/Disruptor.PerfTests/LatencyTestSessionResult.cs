using System;
using System.IO;
using System.Text;
using HdrHistogram;

namespace Disruptor.PerfTests;

public class LatencyTestSessionResult
{
    private readonly Exception _exception;

    public LongHistogram Histogram { get; }
    public TimeSpan Duration { get; set; }
    public int Gen0 { get; set; }
    public int Gen1 { get; set; }
    public int Gen2 { get; set; }

    public LatencyTestSessionResult(LongHistogram histogram, TimeSpan duration, int gen0, int gen1, int gen2)
    {
        Histogram = histogram;
        Duration = duration;
        Gen0 = gen0;
        Gen1 = gen1;
        Gen2 = gen2;
    }

    public LatencyTestSessionResult(Exception exception)
    {
        _exception = exception;
    }

    public void AppendDetailedHtmlReport(int runId, StringBuilder stringBuilder)
    {
        if (_exception != null)
        {
            stringBuilder.AppendLine(" <tr>");
            stringBuilder.AppendLine($"     <td>{runId}</td>");
            stringBuilder.AppendLine($"     <td>FAILED</td>");
            stringBuilder.AppendLine($"     <td>{_exception.Message}</td>");
            stringBuilder.AppendLine($"     <td></td>");
            stringBuilder.AppendLine(" </tr>");
        }
        else
        {
            stringBuilder.AppendLine(" <tr>");
            stringBuilder.AppendLine($"     <td>{runId}</td>");
            stringBuilder.AppendLine($"     <td><pre>");
            using (var writer = new StringWriter(stringBuilder))
            {
                Histogram.OutputPercentileDistribution(writer, 1, 1000.0);
            }
            stringBuilder.AppendLine($"</pre></td>");
            stringBuilder.AppendLine($"     <td>{Duration.TotalMilliseconds:N0} (ms)</td>");
            stringBuilder.AppendLine($"     <td>{Gen0} - {Gen1} - {Gen2}</td>");
            stringBuilder.AppendLine(" </tr>");
        }
    }

    public override string ToString()
    {
        if (_exception != null)
            return $"Run: FAILED: {_exception.Message}";

        return $"Run: Duration: {Duration.TotalMilliseconds:N0} ms - GC: {Gen0} - {Gen1} - {Gen2} - Median: {PFmt(50)} - P75: {PFmt(75)} - P90: {PFmt(90)} - P95: {PFmt(95)} - P99: {PFmt(99)} - P99.9: {PFmt(99.9)} - P99.99: {PFmt(99.99)}";

        string PFmt(double percentile) => $"{P(percentile):N0} ns";
    }

    public long P(double percentile) => Histogram.GetValueAtPercentile(percentile);
}
