using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Disruptor.Tests.Support;
using Disruptor.Util;
using NUnit.Framework;

namespace Disruptor.Tests.Util;

[TestFixture]
public class InternalUtilTests
{
    [TestCase(129, 1)]
    [TestCase(128, 1)]
    [TestCase(127, 2)]
    [TestCase(65, 2)]
    [TestCase(64, 2)]
    [TestCase(63, 3)]
    [TestCase(5, 26)]
    [TestCase(4, 32)]
    [TestCase(3, 43)]
    [TestCase(2, 64)]
    [TestCase(1, 128)]
    public void ShouldGetRingBufferPaddingEventCount(int eventSize, int expectedPadding)
    {
        var padding = InternalUtil.GetRingBufferPaddingEventCount(eventSize);

        Assert.That(padding, Is.EqualTo(expectedPadding));
        Assert.That(padding * eventSize, Is.GreaterThanOrEqualTo(128));
    }

    [Test]
    public void ShouldReadObjectFromArray()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new StubEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.Read<StubEvent>(array, i);

            Assert.That(evt, Is.EqualTo(new StubEvent(i)));
        }
    }

    [TestCase(1)]
    [TestCase(2)]
    [TestCase(5)]
    public void ShouldReadObjectBlockFromArray(int blockSize)
    {
        var array = Enumerable.Range(0, 2000).Select(x => new StubEvent(x)).ToArray();

        for (var i = 0; i < array.Length - blockSize + 1; i++)
        {
            var block = InternalUtil.ReadSpan<StubEvent>(array, i, blockSize);

            Assert.That(block.Length, Is.EqualTo(blockSize));
            for (var dataIndex = 0; dataIndex < blockSize; dataIndex++)
            {
                Assert.That(block[dataIndex], Is.EqualTo(new StubEvent(i + dataIndex)));
            }
        }
    }

    [Test]
    public void ShouldReadValueFromArray()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new StubValueEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.ReadValue<StubValueEvent>(array, i);

            Assert.That(evt, Is.EqualTo(new StubValueEvent(i)));
        }
    }

    [Test]
    public void ShouldReadValueAtIndexUnaligned()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new UnalignedEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.ReadValue<UnalignedEvent>(array, i);

            Assert.That(evt, Is.EqualTo(new UnalignedEvent(i)));
        }
    }

    [Test]
    public void ShouldReadValueFromPointer()
    {
        var index = 0;
        using (var memory = UnmanagedRingBufferMemory.Allocate(2048, () => new StubUnmanagedEvent(index++)))
        {
            for (var i = 0; i < memory.EventCount; i++)
            {
                var evt = InternalUtil.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);

                Assert.That(evt, Is.EqualTo(new StubUnmanagedEvent(i)));
            }
        }
    }

    [Test]
    public void ShouldMutateValueFromArray()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new StubValueEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            ref var evt = ref InternalUtil.ReadValue<StubValueEvent>(array, i);
            evt.TestString = evt.Value.ToString();
        }

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.ReadValue<StubValueEvent>(array, i);

            Assert.That(evt.TestString, Is.EqualTo(evt.Value.ToString()));
        }
    }

    [Test]
    public void ShouldMutateValueFromArrayUnaligned()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new UnalignedEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            ref var evt = ref InternalUtil.ReadValue<UnalignedEvent>(array, i);
            evt.TestString = evt.Value.ToString();
        }

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.ReadValue<UnalignedEvent>(array, i);

            Assert.That(evt.TestString, Is.EqualTo(evt.Value.ToString()));
        }
    }

    [Test]
    public void ShouldMutateValueFromPointer()
    {
        var index = 0;
        using (var memory = UnmanagedRingBufferMemory.Allocate(2048, () => new StubUnmanagedEvent(index++)))
        {
            for (var i = 0; i < memory.EventCount; i++)
            {
                ref var evt = ref InternalUtil.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);
                evt.DoubleValue = evt.Value + 0.1;
            }

            for (var i = 0; i < memory.EventCount; i++)
            {
                var evt = InternalUtil.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);

                Assert.That(evt.DoubleValue, Is.EqualTo(evt.Value + 0.1));
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = 21)]
    public struct UnalignedEvent
    {
        public UnalignedEvent(int value)
        {
            Value = value;
            TestString = null;
        }

        [FieldOffset(0)]
        public string? TestString;

        [FieldOffset(11)]
        public int Value;
    }

    [Test]
    public void ShouldRecomputeArrayDataOffsetWithInternalUtil()
    {
        Console.WriteLine(Environment.Is64BitProcess ? "64BIT" : "32BIT");

        Assert.That(InternalUtil.ArrayDataOffset, Is.EqualTo(InternalUtil.ComputeArrayDataOffset()));
    }

    [Test]
    public void ShouldRecomputeArrayDataOffsetWithMemoryMarshal()
    {
        Console.WriteLine(Environment.Is64BitProcess ? "64BIT" : "32BIT");

        Assert.That(InternalUtil.ArrayDataOffset, Is.EqualTo(ComputeArrayDataOffsetWithMemoryMarshal()));
    }

    private static int ComputeArrayDataOffsetWithMemoryMarshal()
    {
        var methodPointerSize = IntPtr.Size;

        var array = new object[1];

        ref var arrayStart = ref GetArrayStartReference(array);
        ref var firstElement = ref array[0];

        return methodPointerSize + Unsafe.ByteOffset(ref arrayStart, ref firstElement).ToInt32();
    }

    private static ref T GetArrayStartReference<T>(T[] array)
        => ref Unsafe.As<byte, T>(ref Unsafe.As<ByteContainer>(array).Data);

    // ReSharper disable once ClassNeverInstantiated.Local
    private class ByteContainer
    {
        public byte Data;
    }
}
