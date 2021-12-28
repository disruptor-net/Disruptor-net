using System;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;
using Disruptor.Util;

namespace Disruptor.Benchmarks
{
    public class StructProxyBenchmarks
    {
        private IRunner _defaultRunner;
        private IRunner _generatedRunner;

        public StructProxyBenchmarks()
        {
            _defaultRunner = new Runner<IDataProvider<Event>>(new Impl());

            var impl = StructProxy.CreateProxyInstance<IDataProvider<Event>>(new Impl());
            _generatedRunner = (IRunner)Activator.CreateInstance(typeof(Runner<>).MakeGenericType(impl.GetType()), impl);
        }

        [Benchmark(Baseline = true)]
        public int GetDefault() => _defaultRunner.GetData(100).Value;

        [Benchmark]
        public int GetGenerated() => _generatedRunner.GetData(100).Value;

        public interface IRunner
        {
            Event GetData(long sequence);
        }

        public class Runner<TDataProvider> : IRunner
            where TDataProvider : IDataProvider<Event>
        {
            private readonly TDataProvider _dataProvider;

            public Runner(TDataProvider dataProvider)
            {
                _dataProvider = dataProvider;
            }

            public Event GetData(long sequence) => _dataProvider[sequence];
        }

        public class Event
        {
            public int Value;
        }

        public class Impl : IDataProvider<Event>
        {
            private readonly Event _data;
            private readonly Event[] _dataArray;

            public Impl()
            {
                var data = new Event { Value = 42 };
                _data = data;
                _dataArray = new[] { data };
            }

            public Event this[long sequence] => _data;

#if DISRUPTOR_V5
            public ReadOnlySpan<Event> this[long lo, long hi]
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get { return InternalUtil.ReadBlock<Event>(_dataArray, 0, 1); }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public EventBatch<Event> GetBatch(long lo, long hi)
            {
                return new EventBatch<Event>(_dataArray, 0, 1, lo);
            }
#endif
        }
    }
}
