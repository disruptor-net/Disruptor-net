using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Tests.IpcPublisher;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class IpcRingBufferMemoryTests
{
    [Test]
    public async Task ShouldThrowWhenCreatingAnotherDisruptorFromLocalMemory()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        await using var disruptor = new IpcDisruptor<StubUnmanagedEvent>(memory);

        Assert.Throws<InvalidOperationException>(() => _ = new IpcDisruptor<StubUnmanagedEvent>(memory));
    }

#if NET8_0_OR_GREATER
    [Test]
    public async Task ShouldThrowWhenCreatingAnotherDisruptorFromRemoteMemory()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        await using var disruptor = new IpcDisruptor<StubUnmanagedEvent>(memory);

        var process = RemoteIpcPublisher.Start("try-create-ring-buffer", $"--ipc-directory-path {memory.IpcDirectoryPath}");
        Assert.That(process.WaitForExit(5000));
        Assert.That(process.ExitCode, Is.EqualTo(0));
    }
#endif

    [Test]
    public void ShouldThrowWhenCreatingDisruptorFromDisposedMemory()
    {
        var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);
        memory.Dispose();

        Assert.Throws<InvalidOperationException>(() => _ = new IpcDisruptor<StubUnmanagedEvent>(memory));
    }

    [Test]
    public async Task ShouldThrowWhenDisposingMemoryBeforeDisruptor()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        await using var disruptor = new IpcDisruptor<StubUnmanagedEvent>(memory);

        Assert.Throws<InvalidOperationException>(() => memory.Dispose());
    }

    [Test]
    public void ShouldThrowWhenCreatingPublisherFromDisposedMemory()
    {
        var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);
        memory.Dispose();

        Assert.Throws<InvalidOperationException>(() => _ = new IpcPublisher<StubUnmanagedEvent>(memory));
    }

    [Test]
    public void ShouldThrowWhenDisposingMemoryBeforePublisher()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        using var publisher = new IpcPublisher<StubUnmanagedEvent>(memory);

        Assert.Throws<InvalidOperationException>(() => memory.Dispose());
    }

    [Test]
    public void ShouldDeleteTemporaryDirectoryOnFirstMemoryDispose()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        Assert.That(memory.IpcDirectoryPath, Is.Not.Empty);
        Assert.That(memory.IpcDirectoryPath, Does.Exist);

        memory.Dispose();

        Assert.That(memory.IpcDirectoryPath, Does.Not.Exist);
    }

    [Test]
    public void ShouldDeleteTemporaryDirectoryOnSecondMemoryDispose()
    {
        using var memory1 = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(64);

        Assert.That(memory1.IpcDirectoryPath, Is.Not.Empty);
        Assert.That(memory1.IpcDirectoryPath, Does.Exist);

        using var memory2 = IpcRingBufferMemory.Open<StubUnmanagedEvent>(memory1.IpcDirectoryPath);

        memory1.Dispose();

        Assert.That(memory1.IpcDirectoryPath, Does.Exist);

        memory2.Dispose();

        Assert.That(memory1.IpcDirectoryPath, Does.Not.Exist);
    }

    [Test]
    public unsafe void ShouldThrowWhenOpeningMemoryWithInvalidVersion()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<StubUnmanagedEvent>(16);

        *memory.VersionPointer = IpcRingBufferMemory.Version + 1;

        var error = Assert.Throws<InvalidOperationException>(() => _ = IpcRingBufferMemory.Open<StubUnmanagedEvent>(memory.IpcDirectoryPath))!;
        Assert.That(error.Message, Is.EqualTo("Invalid ring buffer memory version"));
    }

    [Test]
    public void ShouldThrowWhenOpeningMemoryWithInvalidEventSize()
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<E1>(16);

        var error = Assert.Throws<InvalidOperationException>(() => _ = IpcRingBufferMemory.Open<StubUnmanagedEvent>(memory.IpcDirectoryPath))!;
        Assert.That(error.Message, Is.EqualTo("Invalid ring buffer memory event size"));
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(64)]
    public unsafe void ShouldAlignSequenceBlocks(int sequenceCapacity)
    {
        using var memory = IpcRingBufferMemory.CreateTemporary<E1>(16, sequencePoolCapacity: sequenceCapacity);

        var aligned = ((long)memory.SequenceBlocks) % 8 == 0;

        Assert.That(aligned, Is.True);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct E1
    {
        public int Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct E2
    {
        public long Id;
    }
}
