using System.Runtime.CompilerServices;

namespace Disruptor
{
    internal static class Constants
    {
        internal const MethodImplOptions AggressiveOptimization = (MethodImplOptions)512;
        internal const int CacheLineSize = 128;
    }
}
