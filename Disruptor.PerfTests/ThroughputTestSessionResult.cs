using System;
using System.Runtime.InteropServices;
using System.Text;

namespace Disruptor.PerfTests
{
    internal class ThroughputTestSessionResult
    {
        private readonly Exception _exception;

        public long TotalOperationsInRun { get; set; }
        public TimeSpan Duration { get; set; }
        public int Gen0 { get; set; }
        public int Gen1 { get; set; }
        public int Gen2 { get; set; }

        public ThroughputTestSessionResult(long totalOperationsInRun, TimeSpan duration, int gen0, int gen1, int gen2)
        {
            TotalOperationsInRun = totalOperationsInRun;
            Duration = duration;
            Gen0 = gen0;
            Gen1 = gen1;
            Gen2 = gen2;
        }

        public ThroughputTestSessionResult(Exception exception)
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
                stringBuilder.AppendLine($"     <td>{TotalOperationsInRun / Duration.TotalSeconds:### ### ### ###}</td>");
                stringBuilder.AppendLine($"     <td>{Duration.TotalMilliseconds:N0} (ms)</td>");
                stringBuilder.AppendLine($"     <td>{Gen0} - {Gen1} - {Gen2}</td>");
                stringBuilder.AppendLine(" </tr>");
            }
        }

        public override string ToString()
        {
            return _exception != null ? $"Run: FAILED: {_exception.Message}" : $"Run: Ops: {TotalOperationsInRun / Duration.TotalSeconds:### ### ### ###} - Duration: {Duration.TotalMilliseconds:N0} (ms) - GC: {Gen0} - {Gen1} - {Gen2}";
        }
    }
}