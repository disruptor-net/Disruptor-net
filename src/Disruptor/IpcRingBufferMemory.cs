using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor;

public unsafe partial class IpcRingBufferMemory : IDisposable
{
    public const int Version = 1;

    private readonly object _lock = new();
    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _dataPointer;
    private bool _instanceHasRingBuffer;
    private int _instancePublisherCount;
    private bool _disposed;

    private IpcRingBufferMemory(string ipcDirectoryPath, MemoryMappedFile mappedFile, MemoryMappedViewAccessor accessor, byte* dataPointer)
    {
        _mappedFile = mappedFile;
        _accessor = accessor;
        _dataPointer = dataPointer;

        IpcDirectoryPath = ipcDirectoryPath;
    }

    public string IpcDirectoryPath { get; }
    public int BufferSize => HeaderPointer->EventCount;

    internal SequencePointer Cursor => GetSequencePointer(0);
    internal IpcSequenceBlock* SequenceBlocks => (IpcSequenceBlock*)(_dataPointer + HeaderPointer->SequencePoolOffset);
    internal int* GatingSequenceIndexArray => (int*)(_dataPointer + HeaderPointer->GatingSequenceIndexArrayOffset);
    internal int GatingSequenceIndexCapacity => HeaderPointer->GatingSequenceIndexCapacity;
    internal int* GatingSequenceIndexCountPointer => &HeaderPointer->GatingSequenceIndexCount;
    internal int* AvailabilityBuffer => (int*)(_dataPointer + HeaderPointer->AvailabilityBufferOffset);
    internal byte* RingBuffer => _dataPointer + HeaderPointer->RingBufferOffset;

    private Header* HeaderPointer => (Header*)_dataPointer;

    internal void RegisterPublisher()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("The ring buffer memory is disposed.");
            }

            _instancePublisherCount++;
        }
    }

    internal void UnregisterPublisher()
    {
        lock (_lock)
        {
            _instancePublisherCount--;
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

            _instanceHasRingBuffer = true;
        }
    }

    internal void UnregisterRingBuffer()
    {
        lock (_lock)
        {
            Volatile.Write(ref HeaderPointer->HasRingBuffer, 0);
            _instanceHasRingBuffer = false;
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

            if (_instanceHasRingBuffer)
            {
                throw new InvalidOperationException("Unable to dispose ring buffer memory before disruptor");
            }

            if (_instancePublisherCount != 0)
            {
                throw new InvalidOperationException("Unable to dispose ring buffer memory before publishers");
            }

            _disposed = true;
        }

        _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
        _dataPointer = null;
        _accessor.Dispose();
        _mappedFile.Dispose();
    }

    private SequencePointer GetSequencePointer(int sequenceIndex)
    {
        var sequenceBlock = (IpcSequenceBlock*)(_dataPointer + HeaderPointer->SequencePoolOffset + sequenceIndex * sizeof(IpcSequenceBlock));
        return new SequencePointer((long*)sequenceBlock);
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

        public int GatingSequenceIndexCapacity => SequenceCapacity;
        public int GatingSequenceIndexArrayOffset => Padding;
        public int GatingSequenceIndexArraySize => GatingSequenceIndexCapacity * sizeof(int);

        public int CursorOffset => SequencePoolOffset;

        public int SequencePoolOffset => GatingSequenceIndexArrayOffset + GatingSequenceIndexArraySize + Padding;
        public int SequencePoolSize => SequenceCapacity * sizeof(IpcSequenceBlock);

        public int AvailabilityBufferOffset => SequencePoolOffset + SequencePoolSize + Padding;
        public int AvailabilityBufferSize => EventCount * sizeof(int);

        public int RingBufferOffset => AvailabilityBufferOffset + AvailabilityBufferSize + Padding;
        public int RingBufferSize => EventCount * EventSize;

        public int FileSize => RingBufferOffset + RingBufferSize + Padding;
    }
}
