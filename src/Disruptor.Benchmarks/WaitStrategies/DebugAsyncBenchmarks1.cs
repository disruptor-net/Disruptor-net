using System;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.WaitStrategies;

[MemoryDiagnoser, ShortRunJob]
public class DebugAsyncBenchmarks1 : IDisposable
{
    private readonly Waiter _waiter = new();

    // [Benchmark]
    public ValueTask<int> Await1()
    {
        return _waiter.Wait();
    }

    // [Benchmark]
    public ValueTask<int> Await2()
    {
        var result = _waiter.Wait();
        while (!result.IsCompleted)
        {
           Thread.Yield();
        }

        return result;
    }

    [Benchmark]
    public void Await3()
    {
        var result = _waiter.Wait();
        while (!result.IsCompleted)
        {
            Thread.Yield();
        }
    }

    public void Dispose()
    {
        _waiter.Dispose();
    }

    private class Waiter : IValueTaskSource<int>, IDisposable
    {
        private readonly Thread _thread;
        private volatile bool _disposed;
        private volatile int _targetVersion = int.MaxValue;
        private ManualResetValueTaskSourceCore<int> _valueTaskSourceCore = new() { RunContinuationsAsynchronously = false };

        public Waiter()
        {
            _thread = new Thread(() => ThreadProc()) { IsBackground = true };
            _thread.Start();
        }

        private void ThreadProc()
        {
            var spinWait = new SpinWait();
            while (!_disposed)
            {
                if (_valueTaskSourceCore.Version != _targetVersion)
                {
                    spinWait.SpinOnce(-1);
                    continue;
                }

                spinWait.Reset();

                Thread.Sleep(1);

                _targetVersion = int.MaxValue;
                _valueTaskSourceCore.SetResult(1);
            }
        }

        public ValueTask<int> Wait()
        {
            _valueTaskSourceCore.Reset();
            _targetVersion = _valueTaskSourceCore.Version;

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

        public void Dispose()
        {
            _disposed = true;
            _thread.Join();
        }
    }
}
