using System.Diagnostics;

namespace Disruptor.PerfTests
{
    public class ThroughputSessionContext
    {
        public readonly Stopwatch Stopwatch = new Stopwatch();
        public double? BatchPercent;
        public double? AverageBatchSize;

        public void Reset()
        {
            Stopwatch.Reset();
            BatchPercent = null;
            AverageBatchSize = null;
        }

        public void Start()
        {
            Stopwatch.Start();
        }

        public void Stop()
        {
            Stopwatch.Stop();
        }

        public void SetBatchData(long batchesProcessedCount, long iterations)
        {
            AverageBatchSize = (double)iterations / batchesProcessedCount;
            BatchPercent = 1 - (double)batchesProcessedCount / iterations;
        }
    }
}