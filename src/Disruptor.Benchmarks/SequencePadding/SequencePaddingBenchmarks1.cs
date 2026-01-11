using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks.SequencePadding;

public class SequencePaddingBenchmarks1 : IDisposable
{
    private readonly UnpaddedSequence _sequence1 = new();
    private readonly Sequence _sequence2 = new();
    private readonly UnpaddedSequence _sequence3 = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly Thread _writerThread1;
    private readonly Thread _writerThread3;

    public SequencePaddingBenchmarks1()
    {
        _writerThread1 = StartWriterThread(_sequence1);
        _writerThread3 = StartWriterThread(_sequence3);
    }

    private Thread StartWriterThread(UnpaddedSequence sequence)
    {
        var thread = new Thread(() =>
        {
            // using var _ = ThreadAffinityUtil.SetThreadAffinity(2, ThreadPriority.Highest);

            var counter = 0L;
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                counter++;
                sequence.SetValue(counter);
            }
        });

        thread.Start();

        return thread;
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _writerThread1.Join();
        _writerThread3.Join();
    }

    [GlobalSetup]
    public void Setup()
    {
        // using var _ = ThreadAffinityUtil.SetThreadAffinity(3, ThreadPriority.Highest);
    }

    [Benchmark]
    public long ReadValue()
    {
        var sum = 0L;
        for (var i = 0; i < 100; i++)
        {
            sum += _sequence2.Value;
        }

        return sum;
    }

    [StructLayout(LayoutKind.Explicit, Size = 56 * 2 + 8)]
    private class Sequence
    {
        /// <summary>
        /// Set to -1 as sequence starting point
        /// </summary>
        public const long InitialCursorValue = -1;

        // padding: DefaultPadding

        [FieldOffset(56)]
        private long _value;

        // padding: DefaultPadding

        /// <summary>
        /// Construct a new sequence counter that can be tracked across threads.
        /// </summary>
        /// <param name="initialValue">initial value for the counter</param>
        public Sequence(long initialValue = InitialCursorValue)
        {
            _value = initialValue;
        }

        /// <summary>
        /// Gets the sequence value.
        /// </summary>
        /// <remarks>
        /// Performs a volatile read of the sequence value.
        /// </remarks>
        public long Value
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Volatile.Read(ref _value);
        }

        /// <summary>
        /// Sets the sequence value.
        /// </summary>
        /// <remarks>
        /// Performs an ordered write of this sequence. The intent is a Store/Store barrier between this write and any previous store.
        /// </remarks>
        /// <param name="value">The new value for the sequence.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetValue(long value)
        {
            Volatile.Write(ref _value, value);
        }
    }
}
