using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Permissions;

namespace Disruptor.Affinity
{
    /// <summary>
    /// Gets and sets the processor affinity of the current thread.
    /// </summary>
    internal static class ProcessorAffinity
    {
        static class Win32Native
        {
            //GetCurrentThread() returns only a pseudo handle. No need for a SafeHandle here.
            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentThread();

            [HostProtection(SelfAffectingThreading = true)]
            [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
            public static extern UIntPtr SetThreadAffinityMask(IntPtr handle, UIntPtr mask);
        }

        public struct ProcessorAffinityHelper : IDisposable
        {
            UIntPtr _lastAffinity;

            internal ProcessorAffinityHelper(UIntPtr lastAffinity)
            {
                _lastAffinity = lastAffinity;
            }

            public void Dispose()
            {
                if (_lastAffinity != UIntPtr.Zero)
                {
                    Win32Native.SetThreadAffinityMask(Win32Native.GetCurrentThread(), _lastAffinity);
                    _lastAffinity = UIntPtr.Zero;
                }
            }
        }

        static ulong MaskFromIds(params int[] ids)
        {
            ulong mask = 0;
            foreach (int id in ids)
            {
                if (id < 0 || id >= Environment.ProcessorCount)
                    throw new ArgumentOutOfRangeException(string.Format("CPUId {0}", id));
                mask |= 1UL << id;
            }
            return mask;
        }

        /// <summary>
        /// Sets a processor affinity mask for the current thread.
        /// </summary>
        /// <param name="mask">A thread affinity mask where each bit set to 1 specifies a logical processor on which this thread is allowed to run. 
        /// <remarks>Note: a thread cannot specify a broader set of CPUs than those specified in the process affinity mask.</remarks> 
        /// </param>
        /// <returns>The previous affinity mask for the current thread.</returns>
        public static UIntPtr SetThreadAffinityMask(UIntPtr mask)
        {
            UIntPtr lastaffinity = Win32Native.SetThreadAffinityMask(Win32Native.GetCurrentThread(), mask);
            if (lastaffinity == UIntPtr.Zero)
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return lastaffinity;
        }

        /// <summary>
        /// Sets the logical CPUs that the current thread is allowed to execute on.
        /// </summary>
        /// <param name="cpuIds">One or more logical processor identifier(s) the current thread is allowed to run on.<remarks>Note: numbering starts from 0.</remarks></param>
        /// <returns>The previous affinity mask for the current thread.</returns>
        public static UIntPtr SetThreadAffinity(params int[] cpuIds)
        {
            return SetThreadAffinityMask(((UIntPtr)MaskFromIds(cpuIds)));
        }

        /// <summary>
        /// Restrict a code block to run on the specified logical CPUs in conjuction with 
        /// the <code>using</code> statement.
        /// </summary>
        /// <param name="cpuIds">One or more logical processor identifier(s) the current thread is allowed to run on.<remarks>Note: numbering starts from 0.</remarks></param>
        /// <returns>A helper structure that will reset the affinity when its Dispose() method is called at the end of the using block.</returns>
        public static ProcessorAffinityHelper BeginAffinity(params int[] cpuIds)
        {
            return new ProcessorAffinityHelper(SetThreadAffinityMask(((UIntPtr)MaskFromIds(cpuIds))));
        }
    }
}