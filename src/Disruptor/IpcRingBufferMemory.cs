using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor;

/// <summary>
/// Represents the shared memory of the IPC components (i.e.: <see cref="IpcDisruptor{T}"/>, <see cref="IpcRingBuffer{T}"/>
/// and <see cref="IpcPublisher{T}"/>).
/// </summary>
/// <remarks>
/// The shared memory is stored in a memory-mapped file located in the specified directory (see <see cref="IpcDirectoryPath"/>).
/// </remarks>
/// <example>
/// <code>
/// // In the first process:
/// using var memory = IpcRingBufferMemory.Create&lt;MyEvent&gt;(path, 1024);
/// await using var disruptor = new IpcDisruptor&lt;MyEvent&gt;(memory, new YieldingWaitStrategy());
/// </code>
/// <code>
/// // In the second process:
/// using var memory = IpcRingBufferMemory.Open&lt;MyEvent&gt;(path);
/// var publisher = new IpcPublisher&lt;MyEvent&gt;(memory);
/// </code>
/// </example>
public unsafe partial class IpcRingBufferMemory : IDisposable
{
    public const int Version = 1;

    private readonly object _lock = new();
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _memoryPointer;
    private bool _hasRingBufferOnMemoryInstance;
    private int _publisherCountOnMemoryInstance;
    private bool _disposed;

    private IpcRingBufferMemory(string ipcDirectoryPath, MemoryMappedFile mappedFile, MemoryMappedViewAccessor accessor, byte* memoryPointer)
    {
        _mappedFile = mappedFile;
        _accessor = accessor;
        _memoryPointer = memoryPointer;

        IpcDirectoryPath = ipcDirectoryPath;
        // Store the value in a field to prevent null-pointer exceptions after dispose.
        BufferSize = HeaderPointer->EventCount;
    }

    /// <summary>
    /// Gets the base directory of the ring buffer memory.
    /// </summary>
    public string IpcDirectoryPath { get; }

    /// <summary>
    /// Gets the ring buffer capacity (number of events).
    /// </summary>
    public int BufferSize { get; }

    internal SequencePointer Cursor => GetSequencePointer(0);
    internal IpcSequenceBlock* SequenceBlocks => (IpcSequenceBlock*)(_memoryPointer + HeaderPointer->SequencePoolOffset);
    internal int* VersionPointer => &HeaderPointer->Version;
    internal int* GatingSequenceIndexArray => (int*)(_memoryPointer + HeaderPointer->GatingSequenceIndexArrayOffset);
    internal int GatingSequenceIndexCapacity => HeaderPointer->GatingSequenceIndexCapacity;
    internal int* GatingSequenceIndexCountPointer => &HeaderPointer->GatingSequenceIndexCount;
    internal int* AvailabilityBuffer => (int*)(_memoryPointer + HeaderPointer->AvailabilityBufferOffset);
    internal byte* RingBuffer => _memoryPointer + HeaderPointer->RingBufferOffset;

    private Header* HeaderPointer => (Header*)_memoryPointer;

    internal void RegisterPublisher()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("The ring buffer memory is disposed.");
            }

            _publisherCountOnMemoryInstance++;
        }
    }

    internal void UnregisterPublisher()
    {
        lock (_lock)
        {
            _publisherCountOnMemoryInstance--;
        }
    }

    internal void RegisterRingBuffer()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("The ring buffer memory is disposed.");
            }

            if (Interlocked.Exchange(ref HeaderPointer->HasRingBuffer, 1) != 0)
            {
                throw new InvalidOperationException($"The ring buffer memory is already owned by another ring buffer.");
            }

            _hasRingBufferOnMemoryInstance = true;
        }
    }

    internal void UnregisterRingBuffer()
    {
        lock (_lock)
        {
            Volatile.Write(ref HeaderPointer->HasRingBuffer, 0);
            _hasRingBufferOnMemoryInstance = false;
        }
    }

    internal SequencePointer NewSequence()
    {
        var sequenceIndex = Interlocked.Increment(ref HeaderPointer->SequenceCount) - 1;
        if (sequenceIndex >= HeaderPointer->SequenceCapacity)
        {
            throw new InvalidOperationException("Sequence capacity reached");
        }

        var sequencePointer = GetSequencePointer(sequenceIndex);
        sequencePointer.SetValue(-1);

        return sequencePointer;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            if (_hasRingBufferOnMemoryInstance)
            {
                throw new InvalidOperationException("Unable to dispose ring buffer memory before disruptor");
            }

            if (_publisherCountOnMemoryInstance != 0)
            {
                throw new InvalidOperationException("Unable to dispose ring buffer memory before publishers");
            }

            _disposed = true;
        }

        var memoryCount = Interlocked.Decrement(ref HeaderPointer->MemoryCount);
        var autoDeleteDirectory = HeaderPointer->AutoDeleteDirectory;

        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _memoryPointer = null;
        _accessor.Dispose();
        _mappedFile.Dispose();

        if (autoDeleteDirectory && memoryCount == 0)
        {
            Directory.Delete(IpcDirectoryPath, true);
        }
    }

    private SequencePointer GetSequencePointer(int sequenceIndex)
    {
        var sequenceBlock = (IpcSequenceBlock*)(_memoryPointer + HeaderPointer->SequencePoolOffset + sequenceIndex * sizeof(IpcSequenceBlock));
        return new SequencePointer((long*)sequenceBlock);
    }

    private static int Align(int value, int alignment)
    {
        return (value + (alignment - 1)) & ~(alignment - 1);
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    private struct Header
    {
        public const int Padding = 128;

        [FieldOffset(0)]
        public int Version;

        [FieldOffset(4)]
        public int EventCount;

        [FieldOffset(8)]
        public int EventSize;

        [FieldOffset(12)]
        public int SequenceCapacity;

        [FieldOffset(16)]
        public int SequenceCount;

        [FieldOffset(20)]
        public int GatingSequenceIndexCount;

        [FieldOffset(24)]
        public int HasRingBuffer;

        /// <summary>
        /// Number of <see cref="IpcRingBufferMemory"/> instances created from the same IPC directory.
        /// </summary>
        [FieldOffset(28)]
        public int MemoryCount;

        [FieldOffset(32)]
        public bool AutoDeleteDirectory;

        public int GatingSequenceIndexCapacity => SequenceCapacity; // Reserve enough space to store all sequences.
        public int GatingSequenceIndexArrayOffset => Padding;
        public int GatingSequenceIndexArraySize => GatingSequenceIndexCapacity * sizeof(int);

        public int SequencePoolOffset => Align(GatingSequenceIndexArrayOffset + GatingSequenceIndexArraySize, 8) + Padding;
        public int SequencePoolSize => SequenceCapacity * sizeof(IpcSequenceBlock);

        public int CursorOffset => SequencePoolOffset; // The cursor is always the first sequence of the pool.

        public int AvailabilityBufferOffset => SequencePoolOffset + SequencePoolSize + Padding;
        public int AvailabilityBufferSize => EventCount * sizeof(int);

        public int RingBufferOffset => AvailabilityBufferOffset + AvailabilityBufferSize + Padding;
        public int RingBufferSize => EventCount * EventSize;

        public int FileSize => RingBufferOffset + RingBufferSize + Padding;
    }
}
