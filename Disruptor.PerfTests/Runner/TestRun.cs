using System.Text;

namespace Disruptor.PerfTests.Runner
{
    public abstract class TestRun
    {
        private readonly PerfTest _perfTest;
        private readonly int _availableCores;

        protected TestRun(int run, PerfTest perfTest, int availableCores)
        {
            _perfTest = perfTest;
            _availableCores = availableCores;
            RunIndex = run;
        }

        protected int RunIndex { get; private set; }
        public abstract void Run();
        protected long DurationInMs { get; set; }
        protected int Gen0Count { get; set; }
        protected int Gen1Count { get; set; }
        protected int Gen2Count { get; set; }

        public void AppendResultHtml(StringBuilder sb)
        {
            sb.AppendLine("                <td>" + RunIndex + "</td>");
            AppendPerfResultHtml(sb);
            sb.AppendLine("                <td>" + DurationInMs + "(ms)</td>");
            sb.AppendLine(string.Format("                <td>{0}-{1}-{2}</td>", Gen0Count, Gen1Count, Gen2Count));
            if(_perfTest.MinimumCoresRequired  > _availableCores)
            {
                sb.AppendLine(string.Format("                <td>WARN: {0} cores required for this test</td>", _perfTest.MinimumCoresRequired));
            }
            else
            {
                sb.AppendLine("                <td>&nbsp;</td>");
            }
        }

        protected abstract void AppendPerfResultHtml(StringBuilder sb);
    }
}