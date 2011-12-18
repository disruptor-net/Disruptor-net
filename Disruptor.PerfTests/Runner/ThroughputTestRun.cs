using System;
using System.Diagnostics;
using System.Text;

namespace Disruptor.PerfTests.Runner
{
    internal class ThroughputTestRun : TestRun
    {
        private readonly ThroughputPerfTest _throughputPerfTest;
        private long _operationsPerSecond;

        public ThroughputTestRun(ThroughputPerfTest throughputPerfTest, int run, int availableCores) : base(run, throughputPerfTest, availableCores)
        {
            _throughputPerfTest = throughputPerfTest;
        }

        public override void Run()
        {
            _throughputPerfTest.PassNumber = RunIndex;
            
            GC.Collect();
            var sw = Stopwatch.StartNew();
            int gen0Count = GC.CollectionCount(0);
            int gen1Count = GC.CollectionCount(1);
            int gen2Count = GC.CollectionCount(2);

            _operationsPerSecond = _throughputPerfTest.RunPass();

            DurationInMs = sw.ElapsedMilliseconds;

            Gen0Count = GC.CollectionCount(0) - gen0Count;
            Gen1Count = GC.CollectionCount(1) - gen1Count;
            Gen2Count = GC.CollectionCount(2) - gen2Count;
            Console.WriteLine("{0}:{1:###,###,###,###}", _throughputPerfTest.GetType().Name, _operationsPerSecond);
        }

        protected override void AppendPerfResultHtml(StringBuilder sb)
        {
            sb.AppendLine(string.Format("                <td>{0:### ### ### ###}ops</td>", _operationsPerSecond));
        }
    }
}