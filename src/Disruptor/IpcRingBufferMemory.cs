using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Disruptor;

public unsafe partial class IpcRingBufferMemory : IDisposable
{
    public const int Version = 1;

    private readonly MemoryMappedFile _mappedFile;
    private readonly MemoryMappedViewAccessor _accessor;
    private byte* _dataPointer;

    private IpcRingBufferMemory(string ipcDirectoryPath, MemoryMappedFile mappedFile, MemoryMappedViewAccessor accessor, byte* dataPointer, bool isOwner)
    {
        _mappedFile = mappedFile;
        _accessor = accessor;
        _dataPointer = dataPointer;

        IpcDirectoryPath = ipcDirectoryPath;
        IsOwner = isOwner;
    }

    public string IpcDirectoryPath { get; }
    public bool IsOwner { get; }

    public int BufferSize => HeaderPointer->EventCount;

    internal SequencePointer Cursor => GetSequencePointer(0);
    internal SequencePointer* GatingSequences => (SequencePointer*)(_dataPointer + HeaderPointer->GatingSequenceBufferOffset);
    internal int GatingSequenceCapacity => HeaderPointer->GatingSequenceBufferSize;
    internal int* GatingSequenceCountPointer => &HeaderPointer->GatingSequenceCount;
    internal int* AvailabilityBuffer => (int*)(_dataPointer + HeaderPointer->AvailabilityBufferOffset);
    internal byte* RingBuffer => _dataPointer + HeaderPointer->RingBufferOffset;

    private Header* HeaderPointer => (Header*)_dataPointer;

    internal SequencePointer NewSequence()
    {
        var sequenceIndex = Interlocked.Increment(ref HeaderPointer->SequenceCount);
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
        if (_dataPointer != null)
        {
            _accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            _dataPointer = null;
            _accessor.Dispose();
            _mappedFile.Dispose();
        }
    }

    private SequencePointer GetSequencePointer(int sequenceIndex)
    {
        var sequenceBlock = (SequenceBlock*)(_dataPointer + HeaderPointer->SequencePoolOffset + sequenceIndex * sizeof(SequenceBlock));
        return new SequencePointer((long*)sequenceBlock);
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    private struct SequenceBlock
    {
        [FieldOffset(0)]
        public long SequenceValue;
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
        public int GatingSequenceCount;

        public int GatingSequenceBufferOffset => sizeof(Header);
        public int GatingSequenceBufferSize => SequenceCapacity * sizeof(long*);

        public int CursorOffset => SequencePoolOffset;

        public int SequencePoolOffset => GatingSequenceBufferOffset + GatingSequenceBufferSize + Padding;
        public int SequencePoolSize => SequenceCapacity * sizeof(SequenceBlock);

        public int AvailabilityBufferOffset => SequencePoolOffset * SequencePoolSize + Padding;
        public int AvailabilityBufferSize => EventCount * sizeof(int);

        public int RingBufferOffset => AvailabilityBufferOffset + AvailabilityBufferSize + Padding;
        public int RingBufferSize => EventCount * EventSize;

        public int FileSize => RingBufferOffset + RingBufferSize + Padding;
    }
}
