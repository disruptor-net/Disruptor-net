using System;
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
}
