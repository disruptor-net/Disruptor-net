using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
#pragma warning disable CS0618 // Type or member is obsolete

namespace Disruptor;

/// <summary>
/// Set of common functions used by the Disruptor
/// </summary>
public static class DisruptorUtil
{
    /// <summary>
    /// Calculate the next power of 2, greater than or equal to x.
    /// </summary>
    /// <param name="x">Value to round up</param>
    /// <returns>The next power of 2 from x inclusive</returns>
    public static int CeilingNextPowerOfTwo(int x)
    {
        var result = 2;

        while (result < x)
        {
            result <<= 1;
        }

        return result;
    }

    /// <summary>
    /// Calculate the log base 2 of the supplied integer, essentially reports the location
    /// of the highest bit.
    /// </summary>
    /// <param name="i">Value to calculate log2 for.</param>
    /// <returns>The log2 value</returns>
    public static int Log2(int i)
    {
        var r = 0;
        while ((i >>= 1) != 0)
        {
            ++r;
        }

        return r;
    }

    /// <summary>
    /// Test whether a given integer is a power of 2
    /// </summary>
    /// <param name="x"></param>
    /// <returns></returns>
    public static bool IsPowerOf2(this int x)
    {
        return x > 0 && (x & (x - 1)) == 0;
    }

    /// <summary>
    /// Get the minimum sequence from an array of <see cref="Sequence"/>s.
    /// </summary>
    /// <param name="sequences">sequences to compare.</param>
    /// <param name="minimum">an initial default minimum.  If the array is empty this value will returned.</param>
    /// <returns>the minimum sequence found or lon.MaxValue if the array is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetMinimumSequence(Sequence[] sequences, long minimum = long.MaxValue)
    {
        for (var i = 0; i < sequences.Length; i++)
        {
            var sequence = sequences[i].Value;
            if (sequence < minimum)
                minimum = sequence;
        }
        return minimum;
    }

    /// <summary>
    /// Get an array of <see cref="Sequence"/>s for the passed <see cref="IEventProcessor"/>s
    /// </summary>
    /// <param name="processors">processors for which to get the sequences</param>
    /// <returns>the array of <see cref="Sequence"/>s</returns>
    public static Sequence[] GetSequencesFor(params IEventProcessor[] processors)
    {
        var sequences = new Sequence[processors.Length];
        for (int i = 0; i < sequences.Length; i++)
        {
            sequences[i] = processors[i].Sequence;
        }

        return sequences;
    }

    /// <summary>
    /// Creates a <see cref="ISequenceWaitStrategy"/> from a <see cref="IWaitStrategy"/>.
    /// </summary>
    [Obsolete(nameof(IWaitStrategy) + " is obsolete.")]
    public static ISequenceWaitStrategy ToSequenceWaitStrategy(this IWaitStrategy waitStrategy)
    {
        return waitStrategy switch
        {
            ISequenceWaitStrategy sequenceWaitStrategy => sequenceWaitStrategy,
            IAsyncWaitStrategy asyncWaitStrategy       => new AsyncWaitStrategyAdapter(asyncWaitStrategy),
            _                                          => new WaitStrategyAdapter(waitStrategy),
        };
    }

    /// <summary>
    /// Creates a <see cref="ISequenceWaiter"/> for a <see cref="IWaitStrategy"/> and a target <see cref="DependentSequenceGroup"/>.
    /// </summary>
    [Obsolete("Please use " + nameof(ISequenceWaitStrategy) + " instead of " + nameof(IWaitStrategy) + ".")]
    public static ISequenceWaiter NewSequenceWaiter(IWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences)
        => new SequenceWaiterAdapter(waitStrategy, dependentSequences);

    /// <summary>
    /// Creates an <see cref="IAsyncSequenceWaiter"/> for an <see cref="IAsyncWaitStrategy"/> and a target <see cref="DependentSequenceGroup"/>.
    /// </summary>
    [Obsolete("Please use " + nameof(IAsyncSequenceWaitStrategy) + " instead of " + nameof(IAsyncWaitStrategy) + ".")]
    public static IAsyncSequenceWaiter NewAsyncSequenceWaiter(IAsyncWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences)
        => new AsyncSequenceWaiterAdapter(waitStrategy, dependentSequences);

    private class WaitStrategyAdapter(IWaitStrategy waitStrategy) : ISequenceWaitStrategy
    {
        public bool IsBlockingStrategy => waitStrategy.IsBlockingStrategy;

        public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
            => new SequenceWaiterAdapter(waitStrategy, dependentSequences);

        public void SignalAllWhenBlocking()
            => waitStrategy.SignalAllWhenBlocking();
    }

    private class AsyncWaitStrategyAdapter(IAsyncWaitStrategy waitStrategy) : IAsyncSequenceWaitStrategy
    {
        public bool IsBlockingStrategy => waitStrategy.IsBlockingStrategy;

        public ISequenceWaiter NewSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
            => new SequenceWaiterAdapter(waitStrategy, dependentSequences);

        public IAsyncSequenceWaiter NewAsyncSequenceWaiter(SequenceWaiterOwner owner, DependentSequenceGroup dependentSequences)
            => new AsyncSequenceWaiterAdapter(waitStrategy, dependentSequences);

        public void SignalAllWhenBlocking()
            => waitStrategy.SignalAllWhenBlocking();
    }

    private class SequenceWaiterAdapter(IWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences) : ISequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public SequenceWaitResult WaitFor(long sequence, CancellationToken cancellationToken)
            => waitStrategy.WaitFor(sequence, dependentSequences, cancellationToken);

        public void Cancel()
            => waitStrategy.SignalAllWhenBlocking();

        public void Dispose()
        {
        }
    }

    private class AsyncSequenceWaiterAdapter(IAsyncWaitStrategy waitStrategy, DependentSequenceGroup dependentSequences) : IAsyncSequenceWaiter
    {
        public DependentSequenceGroup DependentSequences => dependentSequences;

        public ValueTask<SequenceWaitResult> WaitForAsync(long sequence, CancellationToken cancellationToken)
            => waitStrategy.WaitForAsync(sequence, dependentSequences, cancellationToken);

        public void Cancel()
            => waitStrategy.SignalAllWhenBlocking();

        public void Dispose()
        {
        }
    }
}
