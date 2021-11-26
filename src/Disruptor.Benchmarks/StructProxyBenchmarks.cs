using System;
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
            public readonly Event Data = new Event { Value = 42 };

            public Event this[long sequence] => Data;
        }
    }
}
