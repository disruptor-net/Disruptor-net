using System;
using System.Runtime.CompilerServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks
{
    [DisassemblyDiagnoser()]
    public class CancellationTokenBenchmarks
    {
        private readonly ISequenceBarrierRef _sequenceBarrier;
        private readonly CancellationTokenSource _cancellationTokenSource;

        public CancellationTokenBenchmarks()
        {
            _sequenceBarrier = new SequenceBarrierRef();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Benchmark(OperationsPerInvoke = 100)]
        public void WaitUsingSequenceBarrier()
        {
            WaitImpl(_sequenceBarrier);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WaitImpl(ISequenceBarrierRef sequenceBarrier)
        {
            for (var i = 0; i < 100; i++)
            {
                sequenceBarrier.CheckAlert();
            }
        }

        [Benchmark(OperationsPerInvoke = 100)]
        public void WaitUsingCancellationToken()
        {
            WaitImpl(_cancellationTokenSource.Token);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void WaitImpl(CancellationToken cancellationToken)
        {
            for (var i = 0; i < 100; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        public interface ISequenceBarrierRef
        {
            void CheckAlert();
        }

        public class SequenceBarrierRef : ISequenceBarrierRef
        {
            private volatile bool _alerted;

            public void Alert()
            {
                _alerted = true;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void CheckAlert()
            {
                if (_alerted)
                {
                    AlertExceptionRef.Throw();
                }
            }
        }

        public class AlertExceptionRef : Exception
        {
            public static readonly AlertExceptionRef Instance = new();

            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void Throw()
            {
                throw Instance;
            }
        }
    }
}
