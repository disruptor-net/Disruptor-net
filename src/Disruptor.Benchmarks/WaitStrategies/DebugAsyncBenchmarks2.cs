using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser, ShortRunJob]
public class DebugAsyncBenchmarks2
{
    private readonly Waiter _waiter = new();

    [Benchmark]
    public ValueTask<int> Await1()
    {
        return _waiter.Wait();
    }

    private class Waiter : IValueTaskSource<int>
    {
        private ManualResetValueTaskSourceCore<int> _valueTaskSourceCore = new() { RunContinuationsAsynchronously = false };

        public ValueTask<int> Wait()
        {
            _valueTaskSourceCore.Reset();
            _valueTaskSourceCore.SetResult(1);

            return new ValueTask<int>(this, _valueTaskSourceCore.Version);
        }

        public int GetResult(short token)
        {
            return _valueTaskSourceCore.GetResult(token);
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _valueTaskSourceCore.GetStatus(token);
        }

        public void OnCompleted(Action<object> continuation, object state, short token, ValueTaskSourceOnCompletedFlags flags)
        {
            _valueTaskSourceCore.OnCompleted(continuation, state, token, flags);
        }
    }
}
