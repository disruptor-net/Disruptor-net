using System;

namespace Disruptor.Util;

/// <summary>
/// Expose non-inlinable methods to throw exceptions.
/// </summary>
internal static class ThrowHelper
{
    public static void ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize()
    {
        throw new ArgumentException("n must be > 0 and <= bufferSize");
    }

    public static void ThrowArgumentOutOfRangeException()
    {
        throw new ArgumentOutOfRangeException();
    }
}
