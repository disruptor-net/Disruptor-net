using System;
using System.Runtime.CompilerServices;

namespace Disruptor.Util
{
    /// <summary>
    /// Expose non-inlinable methods to throw exceptions.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgMustBeGreaterThanZeroAndLessThanBufferSize()
        {
            throw new ArgumentException("n must be > 0 and <= bufferSize");
        }
    }
}
