using System;
using System.Runtime.CompilerServices;

namespace Disruptor
{
    /// <summary>
    /// Expose non-inlinable methods to throw exceptions.
    /// </summary>
    internal static class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowArgMustBeGreaterThanZero()
        {
            throw new ArgumentException("n must be > 0");
        }
    }
}
