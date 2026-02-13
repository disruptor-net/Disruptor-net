using System.Runtime.CompilerServices;

namespace Disruptor.Util;

internal static class Constants
{
    internal const MethodImplOptions AggressiveOptimization = (MethodImplOptions)512;

    /// <summary>
    /// Default padding that should be used to prevent false sharing.
    /// </summary>
    internal const int DefaultPadding = 56;

    public const string TypeOrMethodNotReachableForAot = "The type or method is not reachable with AOT";
    public const string DynamicCodeNotReachableWithAot = "The dynamic code path is not reachable with AOT";
}
