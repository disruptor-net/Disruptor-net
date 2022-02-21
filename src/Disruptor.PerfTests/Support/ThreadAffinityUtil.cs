using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;

namespace Disruptor.PerfTests.Support;

public class ThreadAffinityUtil
{
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("libc.so.6")]
    private static extern int sched_setaffinity(int pid, IntPtr cpusetsize, ref ulong cpuset);

    public static Scope SetThreadAffinity(int processorIndex)
    {
        Thread.BeginThreadAffinity();

        var affinity = (1u << processorIndex);
        SetProcessorAffinity(affinity);

        return new Scope();
    }

    private static void RemoveThreadAffinity()
    {
        var affinity = (1u << Environment.ProcessorCount) - 1;
        SetProcessorAffinity(affinity);

        Thread.EndThreadAffinity();
    }

    private static void SetProcessorAffinity(uint mask)
    {
#if NETFRAMEWORK
        SetProcessorAffinityWindows(mask);
#else
        if (OperatingSystem.IsWindows())
            SetProcessorAffinityWindows(mask);
        else if (OperatingSystem.IsLinux())
            SetProcessorAffinityLinux(mask);
        else
            throw new PlatformNotSupportedException();
#endif
    }

#if NETCOREAPP
    [SupportedOSPlatform("windows")]
#endif
    private static void SetProcessorAffinityWindows(uint mask)
    {
        var processThread = GetCurrentProcessThread();
        processThread.ProcessorAffinity = new IntPtr(mask);
    }

#if NETCOREAPP
    [SupportedOSPlatform("linux")]
    private static void SetProcessorAffinityLinux(uint mask)
    {
        var cpuset = (ulong)mask;
        sched_setaffinity(0, new IntPtr(sizeof(ulong)), ref cpuset);
    }
#endif

    private static ProcessThread GetCurrentProcessThread()
    {
        var threadId = GetCurrentThreadId();

        foreach (ProcessThread processThread in Process.GetCurrentProcess().Threads)
        {
            if (processThread.Id == threadId)
            {
                return processThread;
            }
        }

        throw new InvalidOperationException($"Could not retrieve native thread with ID: {threadId}, current managed thread ID was {threadId}");
    }

    public readonly ref struct Scope
    {
        public void Dispose()
        {
            RemoveThreadAffinity();
        }
    }
}
