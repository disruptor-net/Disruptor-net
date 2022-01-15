using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    public class ExceptionBenchmarks
    {
        [Params(10, 100, 1000, 1000000)]
        public int IterationCount { get; set; }

        [Benchmark]
        public int ThrowAndCatchNewException()
        {
            try
            {
                for (var i = 0; i < IterationCount; i++)
                {
                    TestAndThrowNew(i);
                }

                return 0;
            }
            catch (XException)
            {
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TestAndThrowNew(int i)
        {
            if (i == IterationCount - 1)
                throw new XException();
        }

        [Benchmark]
        public int ThrowAndCatchSingletonException()
        {
            try
            {
                for (var i = 0; i < IterationCount; i++)
                {
                    TestAndThrowSingleton(i);
                }

                return 0;
            }
            catch (XException)
            {
                return 1;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void TestAndThrowSingleton(int i)
        {
            if (i == IterationCount - 1)
                throw XException.Instance;
        }

        [Benchmark]
        public int NoException()
        {
            for (var i = 0; i < IterationCount; i++)
            {
                if (TestAndReturn(i) < 0)
                    return 1;
            }

            return 0;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private int TestAndReturn(int i)
        {
            if (i == IterationCount - 1)
                return -1;

            return i;
        }

        public class XException : Exception
        {
            public static readonly XException Instance = new();
        }
    }
}
