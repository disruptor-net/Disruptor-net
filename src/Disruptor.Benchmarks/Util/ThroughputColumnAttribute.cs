using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Mathematics;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace Disruptor.Benchmarks.Util;

public class ThroughputColumnAttribute : ColumnConfigBaseAttribute
{
    public ThroughputColumnAttribute()
        : base(new Column())
    {
    }

    private class Column : IColumn
    {
        public string Id => "StatisticColumn." + ColumnName;

        public string ColumnName => "Op/us";

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase) => Format(summary[benchmarkCase].ResultStatistics, SummaryStyle.Default);

        public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style) => Format(summary[benchmarkCase].ResultStatistics, style);

        public bool IsAvailable(Summary summary) => true;

        public bool AlwaysShow => true;

        public ColumnCategory Category => ColumnCategory.Statistics;

        public int PriorityInCategory => 3;

        public bool IsNumeric => true;

        public UnitType UnitType => UnitType.Dimensionless;

        public string Legend => "Operation per microsecond";

        public override string ToString() => ColumnName;

        public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

        private static string Format(Statistics statistics, SummaryStyle style)
        {
            if (statistics == null)
                return "NA";

            var d = 1000.0 / statistics.Mean;
            return double.IsNaN(d) ? "NA" : d.ToString("N2", style.CultureInfo);
        }
    }
}