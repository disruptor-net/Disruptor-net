using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using static InlineIL.IL.Emit;

namespace Disruptor
{
    /// <summary>
    /// Disposable block of unmanaged memory to store events.
    /// </summary>
    public class UnmanagedRingBufferMemory : IDisposable
    {
        private readonly SafeHandle _memoryHandle;

        public UnmanagedRingBufferMemory(SafeHandle memoryHandle, int padding, int eventCount, int eventSize)
        {
            _memoryHandle = memoryHandle;
            PointerToFirstEvent = memoryHandle.DangerousGetHandle() + padding;
            EventCount = eventCount;
            EventSize = eventSize;
        }

        public IntPtr PointerToFirstEvent { get; }
        public int EventCount { get; }
        public int EventSize { get; }

        public void Dispose()
        {
            _memoryHandle.Dispose();
        }

        public unsafe T[] ToArray<T>()
            where T : unmanaged
        {
            var array = new T[EventCount];
            var pointerToFirstEvent = (T*)PointerToFirstEvent;

            for (var i = 0; i < array.Length; i++)
            {
                array[i] = pointerToFirstEvent[i];
            }

            return array;
        }

        /// <summary>
        /// Allocate a block of unmanaged memory to store events.
        /// </summary>
        /// <param name="eventCount">number of event to store</param>
        /// <param name="eventSize">size of each event</param>
        /// <returns></returns>
        public static UnmanagedRingBufferMemory Allocate(int eventCount, int eventSize)
        {
            if (eventCount < 1)
            {
                throw new ArgumentException($"{nameof(eventCount)} must not be less than 1");
            }
            if (!eventCount.IsPowerOf2())
            {
                throw new ArgumentException($"{nameof(eventCount)} must be a power of 2");
            }
            if (eventSize < 1)
            {
                throw new ArgumentException($"{nameof(eventSize)} must not be less than 1");
            }

            var size = eventCount * eventSize + Util.RingBufferPaddingBytes * 2;
            var pointer = Marshal.AllocHGlobal(size);

            InitBlock(pointer, 0, (uint)size);

            var handle = new AllocSafeHandle(pointer);

            return new UnmanagedRingBufferMemory(handle, Util.RingBufferPaddingBytes, eventCount, eventSize);
        }

        /// <summary>
        /// Allocate a block of unmanaged memory to store events.
        /// </summary>
        /// <param name="eventCount">number of event to store</param>
        /// <param name="factory">event factory method</param>
        /// <returns></returns>
        public static unsafe UnmanagedRingBufferMemory Allocate<T>(int eventCount, Func<T> factory)
            where T : unmanaged
        {
            var memory = Allocate(eventCount, sizeof(T));
            var pointer = (T*)memory.PointerToFirstEvent;

            for (var i = 0; i < eventCount; i++)
            {
                pointer[i] = factory();
            }

            return memory;
        }

        private static void InitBlock(IntPtr startAddress, byte value, uint byteCount)
        {
            Ldarg(nameof(startAddress));
            Ldarg(nameof(value));
            Ldarg(nameof(byteCount));
            Unaligned(1);
            Initblk();
        }

        private class AllocSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public AllocSafeHandle(IntPtr handle) : base(true)
            {
                SetHandle(handle);
            }

            protected override bool ReleaseHandle()
            {
                Marshal.FreeHGlobal(handle);

                return true;
            }
        }
    }
}
