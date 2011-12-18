using System;
using System.Diagnostics;
using Disruptor.Collections;

namespace Disruptor.PerfTests.Pipeline3StepLatency
{
    public abstract class AbstractPipeline3StepLatencyPerfTest :LatencyPerfTest
    {
        protected const int Size = 1024 * 32;
        protected const long PauseNanos = 1000;
        protected static readonly double TicksToNanos = 1000 * 1000 * 1000 / (double)Stopwatch.Frequency;
        private Histogram _histogram;
        private static long _stopwatchTimestampCostInNano;

        protected AbstractPipeline3StepLatencyPerfTest(int iterations) : base(iterations)
        {
        }

        public override Histogram Histogram
        {
            get
            {
                if (_histogram == null)
                {
                    var intervals = new long[31];
                    var intervalUpperBound = 1L;
                    for (var i = 0; i < intervals.Length - 1; i++)
                    {
                        intervalUpperBound *= 2;
                        intervals[i] = intervalUpperBound;
                    }

                    intervals[intervals.Length - 1] = long.MaxValue;
                    _histogram = new Histogram(intervals);
                }
                return _histogram;
            }
        }

        protected static long StopwatchTimestampCostInNano
        {
            get
            {
                if (_stopwatchTimestampCostInNano == 0)
                {
                    const long iterations = 10 * 1000 * 1000;
                    var start = Stopwatch.GetTimestamp();
                    var finish = start;

                    for (var i = 0; i < iterations; i++)
                    {
                        finish = Stopwatch.GetTimestamp();
                    }

                    if (finish <= start)
                    {
                        throw new Exception();
                    }

                    finish = Stopwatch.GetTimestamp();
                    _stopwatchTimestampCostInNano = (long)(((finish - start) / (double)iterations) * TicksToNanos);
                }
                return _stopwatchTimestampCostInNano;
            }
        }

        public override int MinimumCoresRequired
        {
            get { return 4; }
        }
    }
}