using System;
using System.Threading;

namespace Disruptor.PerfTests.Support;

public readonly struct ThreadAffinityScope(bool hasAffinity, ThreadPriority? initialThreadPriority) : IDisposable
{
    public void Dispose()
    {
        if (hasAffinity) // It would be cleaner to restore previous affinity.
            ThreadAffinityUtil.RemoveThreadAffinity();

        if (initialThreadPriority != null)
            Thread.CurrentThread.Priority = initialThreadPriority.Value;
    }
}
