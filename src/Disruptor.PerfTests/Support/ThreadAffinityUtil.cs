using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor.PerfTests.Support
{
    public class ThreadAffinityUtil
    {
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        public static Scope SetThreadAffinity(int processorIndex)
        {
            // notify the runtime we are going to use affinity
            Thread.BeginThreadAffinity();

            // we can now safely access the corresponding native thread
            var processThread = CurrentProcessThread;

            var affinity = (1 << processorIndex);

            processThread.ProcessorAffinity = new IntPtr(affinity);

            return new Scope();
        }

        private static void RemoveThreadAffinity()
        {
            var processThread = CurrentProcessThread;

            var affinity = (1 << Environment.ProcessorCount) - 1;

            processThread.ProcessorAffinity = new IntPtr(affinity);

            Thread.EndThreadAffinity();
        }

        private static ProcessThread CurrentProcessThread
        {
            get
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
        }

        public readonly ref struct Scope
        {
            public void Dispose()
            {
                RemoveThreadAffinity();
            }
        }
    }
}
