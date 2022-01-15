using System;
using System.Linq;
using System.Runtime.InteropServices;
using Disruptor.Tests.Support;
using Disruptor.Util;
using InlineIL;
using NUnit.Framework;
using static InlineIL.IL.Emit;

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

        Assert.AreEqual(expectedPadding, padding);
        Assert.GreaterOrEqual(padding * eventSize, 128);
    }

    [Test]
    public void ShouldReadObjectFromArray()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new StubEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.Read<StubEvent>(array, i);

            Assert.AreEqual(new StubEvent(i), evt);
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

            Assert.AreEqual(blockSize, block.Length);
            for (var dataIndex = 0; dataIndex < blockSize; dataIndex++)
            {
                Assert.AreEqual(new StubEvent(i + dataIndex), block[dataIndex]);
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

            Assert.AreEqual(new StubValueEvent(i), evt);
        }
    }

    [Test]
    public void ShouldReadValueAtIndexUnaligned()
    {
        var array = Enumerable.Range(0, 2000).Select(x => new UnalignedEvent(x)).ToArray();

        for (var i = 0; i < array.Length; i++)
        {
            var evt = InternalUtil.ReadValue<UnalignedEvent>(array, i);

            Assert.AreEqual(new UnalignedEvent(i), evt);
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

                Assert.AreEqual(new StubUnmanagedEvent(i), evt);
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

            Assert.AreEqual(evt.Value.ToString(), evt.TestString);
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

            Assert.AreEqual(evt.Value.ToString(), evt.TestString);
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

                Assert.AreEqual(evt.Value + 0.1, evt.DoubleValue);
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
    public void ShouldGetArrayDataOffset()
    {
        Console.WriteLine(Environment.Is64BitProcess ? "64BIT" : "32BIT");

        Assert.AreEqual(ComputeArrayDataOffset(), InternalUtil.ArrayDataOffset);
    }

    private static int ComputeArrayDataOffset()
    {
        var array = new object[1];

        return (int)GetElementOffset(array, ref array[0]);
    }

    private static IntPtr GetElementOffset(object origin, ref object target)
    {
        IL.DeclareLocals(false, typeof(byte).MakeByRefType());

        Ldarg(nameof(target));

        Ldarg(nameof(origin)); // load the object
        Stloc_0(); // convert the object pointer to a byref
        Ldloc_0(); // load the object pointer as a byref

        Sub();

        return IL.Return<IntPtr>();
    }
}