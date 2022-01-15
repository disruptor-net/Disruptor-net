using System;
using System.Runtime.CompilerServices;
using Disruptor.Processing;

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
    public static long GetMinimumSequence(ISequence[] sequences, long minimum = long.MaxValue)
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
    public static ISequence[] GetSequencesFor(params IEventProcessor[] processors)
    {
        var sequences = new ISequence[processors.Length];
        for (int i = 0; i < sequences.Length; i++)
        {
            sequences[i] = processors[i].Sequence;
        }

        return sequences;
    }
}