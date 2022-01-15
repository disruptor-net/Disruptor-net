using System.Runtime.CompilerServices;

namespace Disruptor.Util;

internal static class Constants
{
    internal const MethodImplOptions AggressiveOptimization = (MethodImplOptions)512;

    /// <summary>
    /// Default padding that should be used to prevent false sharing.
    /// </summary>
    internal const int DefaultPadding = 56;
}