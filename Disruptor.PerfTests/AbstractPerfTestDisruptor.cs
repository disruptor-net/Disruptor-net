using System;

namespace Disruptor.PerfTests
{
    public abstract class AbstractPerfTestDisruptor
    {
        private const int _runs = 7;

        protected void TestImplementations()
        {
            var availableProcessors = Environment.ProcessorCount;
            if (RequiredProcessorCount > availableProcessors)
            {
                Console.WriteLine("*** Warning ***: your system has insufficient processors to execute the test efficiently. ");
                Console.WriteLine($"Processors required = {RequiredProcessorCount}, available = {availableProcessors}");
            }

            var disruptorOps = new long[_runs];

            Console.WriteLine("Starting Disruptor tests");
            for (var i = 0; i < _runs; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();

                disruptorOps[i] = RunDisruptorPass();
                Console.WriteLine($"Run {i}, Disruptor={disruptorOps[i]} ops/sec");
            }
        }

        protected abstract int RequiredProcessorCount { get; }

        protected abstract long RunDisruptorPass();
        
        public static void PrintResults(string className, long[] disruptorOps, long[] queueOps)
        {
            for (var i = 0; i < _runs; i++)
            {
                Console.WriteLine($"{className} run {i}: BlockingQueue={queueOps[i]} Disruptor={disruptorOps} ops/sec\n");
            }
        }
    }
}