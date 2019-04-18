using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Environments;

namespace Disruptor.PerfTests
{
    public class ComputerSpecifications
    {
        private readonly HostEnvironmentInfo _hostEnvironmentInfo;

        public ComputerSpecifications()
        {
            _hostEnvironmentInfo = HostEnvironmentInfo.GetCurrent();
        }

        public int? PhysicalCoreCount => _hostEnvironmentInfo.CpuInfo.Value.PhysicalCoreCount;
        public int? LogicalCoreCount => _hostEnvironmentInfo.CpuInfo.Value.LogicalCoreCount;
        public bool IsHyperThreaded => LogicalCoreCount > PhysicalCoreCount;

        public override string ToString()
        {
            var builder = new StringBuilder();

            foreach (var line in GetLines())
            {
                builder.AppendLine(line);
            }

            return builder.ToString();
        }

        public void AppendHtml(StringBuilder builder)
        {
            foreach (var line in GetLines())
            {
                builder.Append(line);
                builder.AppendLine("<br>");
            }
        }

        private IEnumerable<string> GetLines()
        {
            yield return "OS = " + _hostEnvironmentInfo.OsVersion.Value;

            foreach (var line in _hostEnvironmentInfo.ToFormattedString().Skip(1))
            {
                yield return line;
            }
        }
    }
}
