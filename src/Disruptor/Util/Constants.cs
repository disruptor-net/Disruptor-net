using System.Runtime.CompilerServices;

namespace Disruptor
{
    internal static class Constants
    {
        internal const MethodImplOptions AggressiveOptimization = (MethodImplOptions)512;

        internal const string ObsoletePublicationApiMessage = @"Use new publication API instead:
using (var scope = ringBuffer.PublishEvent())
{
    // The event will be published on disposing.
    var e = scope.Event();
}";
    }
}
