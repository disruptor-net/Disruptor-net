using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

unsafe partial class IpcRingBufferMemory
{
    /// <summary>
    /// Creates a new ring buffer memory in a temporary directory.
    /// </summary>
    /// <param name="bufferSize">size of the ring buffer</param>
    /// <param name="sequencePoolCapacity">maximum number of sequences that can be allocated from the memory</param>
    /// <param name="initializer">event initializer</param>
    /// <typeparam name="T">type of the ring buffer events</typeparam>
    public static IpcRingBufferMemory CreateTemporary<T>(int bufferSize, int sequencePoolCapacity = 64, Func<int, T>? initializer = null)
        where T : unmanaged
    {
        var ipcDirectoryPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        return CreateNew<T>(ipcDirectoryPath, bufferSize, sequencePoolCapacity, initializer, autoDeleteDirectory: true);
    }

    /// <summary>
    /// Creates a new ring buffer memory in the specified directory.
    /// If the directory already exists, it will be deleted.
    /// </summary>
    /// <param name="ipcDirectoryPath">base directory of the ring buffer memory</param>
    /// <param name="bufferSize">size of the ring buffer; must be a power of 2</param>
    /// <param name="sequencePoolCapacity">maximum number of sequences that can be allocated from the memory</param>
    /// <param name="initializer">event initializer</param>
    /// <param name="autoDeleteDirectory">indicates whether the directory must be deleted on dispose</param>
    /// <typeparam name="T">type of the ring buffer events</typeparam>
    public static IpcRingBufferMemory Create<T>(string ipcDirectoryPath, int bufferSize, int sequencePoolCapacity = 64, Func<int, T>? initializer = null, bool autoDeleteDirectory = false)
        where T : unmanaged
    {
        if (bufferSize < 1)
        {
            throw new ArgumentException("bufferSize must not be less than 1");
        }

        if (!bufferSize.IsPowerOf2())
        {
            throw new ArgumentException("bufferSize must be a power of 2");
        }

        if (Directory.Exists(ipcDirectoryPath))
        {
            Directory.Delete(ipcDirectoryPath, true);
        }

        return CreateImpl<T>(ipcDirectoryPath, bufferSize, sequencePoolCapacity, initializer, autoDeleteDirectory);
    }

    /// <summary>
    /// Creates a new ring buffer memory in the specified directory.
    /// </summary>
    /// <throws cref="InvalidOperationException">if the directory already exists</throws>
    /// <param name="ipcDirectoryPath">base directory of the ring buffer memory</param>
    /// <param name="bufferSize">size of the ring buffer; must be a power of 2</param>
    /// <param name="sequencePoolCapacity">maximum number of sequences that can be allocated from the memory</param>
    /// <param name="initializer">event initializer</param>
    /// <param name="autoDeleteDirectory">indicates whether the directory must be deleted on dispose</param>
    /// <typeparam name="T">type of the ring buffer events</typeparam>
    public static IpcRingBufferMemory CreateNew<T>(string ipcDirectoryPath, int bufferSize, int sequencePoolCapacity = 64, Func<int, T>? initializer = null, bool autoDeleteDirectory = false)
        where T : unmanaged
    {
        if (bufferSize < 1)
        {
            throw new ArgumentException("bufferSize must not be less than 1");
        }

        if (!bufferSize.IsPowerOf2())
        {
            throw new ArgumentException("bufferSize must be a power of 2");
        }

        if (Directory.Exists(ipcDirectoryPath))
        {
            throw new InvalidOperationException("Ring buffer directory already exists");
        }

        return CreateImpl<T>(ipcDirectoryPath, bufferSize, sequencePoolCapacity, initializer, autoDeleteDirectory);
    }

    private static IpcRingBufferMemory CreateImpl<T>(string ipcDirectoryPath, int bufferSize, int sequencePoolCapacity, Func<int, T>? initializer, bool autoDeleteDirectory)
        where T : unmanaged
    {
        Directory.CreateDirectory(ipcDirectoryPath);

        var header = new Header
        {
            Version = Version,
            EventCount = bufferSize,
            EventSize = sizeof(T),
            SequenceCapacity = sequencePoolCapacity,
            SequenceCount = 1, // The first sequence is reserved for the cursor.
            GatingSequenceIndexCount = 0,
            AutoDeleteDirectory = true,
            MemoryCount = 1,
        };

        var filePath = GetRingBufferFile(ipcDirectoryPath);
        var fileStream = File.Open(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
        var mappedFile = MemoryMappedFile.CreateFromFile(fileStream, mapName: null, header.FileSize, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
        var accessor = mappedFile.CreateViewAccessor();

        var memoryPointer = (byte*)null;

        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref memoryPointer);
        if (memoryPointer == null)
        {
            DisposeAndThrow(mappedFile, accessor, "Could not acquire shared memory pointer");
        }

        InitializeMemory(header, memoryPointer, initializer);

        return new IpcRingBufferMemory(ipcDirectoryPath, mappedFile, accessor, memoryPointer);
    }

    /// <remarks>
    /// Passing header by ref would be more efficient, but the optimization is not needed in this context.
    /// </remarks>
    private static void InitializeMemory<T>(Header header, byte* memoryPointer, Func<int, T>? initializer) where T : unmanaged
    {
        Unsafe.InitBlock(memoryPointer, 0, (uint)header.EventCount);

        // Write header.
        *(Header*)memoryPointer = header;

        // Write initial cursor value.
        *(long*)(memoryPointer + header.CursorOffset) = -1;

        // Initialize availability buffer.
        new Span<int>(memoryPointer + header.AvailabilityBufferOffset, header.EventCount).Fill(-1);

        if (initializer != null)
        {
            // Initialize ring buffer events.
            var ringBuffer = (T*)(memoryPointer + header.RingBufferOffset);
            for (var i = 0; i < header.EventCount; i++)
            {
                ringBuffer[i] = initializer(i);
            }
        }
    }

    /// <summary>
    /// Opens a ring buffer memory in the specified base directory. The ring buffer memory must have been created beforehand with
    /// <see cref="Create{T}"/> or <see cref="CreateNew{T}"/>.
    /// </summary>
    /// <typeparam name="T">type of the ring buffer events</typeparam>
    public static IpcRingBufferMemory Open<T>(string ipcDirectoryPath)
        where T : unmanaged
    {
        if (!Directory.Exists(ipcDirectoryPath))
        {
            throw new InvalidOperationException("Ring buffer directory does not exist");
        }

        var filePath = GetRingBufferFile(ipcDirectoryPath);
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("Ring buffer file does not exist");
        }

        var fileStream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        var mappedFile = MemoryMappedFile.CreateFromFile(fileStream, mapName: null, 0, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, leaveOpen: false);
        var accessor = mappedFile.CreateViewAccessor();

        var memoryPointer = (byte*)null;
        accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref memoryPointer);

        if (memoryPointer == null)
        {
            DisposeAndThrow(mappedFile, accessor, "Could not acquire shared memory pointer");
        }

        var headerPointer = (Header*)memoryPointer;
        if (headerPointer->Version != Version)
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            DisposeAndThrow(mappedFile, accessor, "Invalid ring buffer memory version");
        }

        if (headerPointer->EventSize != sizeof(T))
        {
            accessor.SafeMemoryMappedViewHandle.ReleasePointer();
            DisposeAndThrow(mappedFile, accessor, "Invalid ring buffer memory event size");
        }

        Interlocked.Increment(ref headerPointer->MemoryCount);

        return new IpcRingBufferMemory(ipcDirectoryPath, mappedFile, accessor, memoryPointer);
    }

    private static void DisposeAndThrow(MemoryMappedFile mappedFile, MemoryMappedViewAccessor accessor, string message)
    {
        accessor.Dispose();
        mappedFile.Dispose();
        throw new InvalidOperationException(message);
    }

    private static string GetRingBufferFile(string ringBufferDirectoryPath)
    {
        return Path.Combine(ringBufferDirectoryPath, "buffer.dat");
    }
}
