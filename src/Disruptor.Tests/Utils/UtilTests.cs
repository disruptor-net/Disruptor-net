using System.Linq;
using System.Runtime.InteropServices;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Utils
{
    [TestFixture]
    public class UtilTests
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
            var padding = Util.GetRingBufferPaddingEventCount(eventSize);

            Assert.AreEqual(expectedPadding, padding);
            Assert.GreaterOrEqual(padding * eventSize, 128);
        }

        [Test]
        public void ShouldReturnNextPowerOfTwo()
        {
            var powerOfTwo = 1000.CeilingNextPowerOfTwo();

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnExactPowerOfTwo()
        {
            var powerOfTwo = 1024.CeilingNextPowerOfTwo();

            Assert.AreEqual(1024, powerOfTwo);
        }

        [Test]
        public void ShouldReturnMinimumSequence()
        {
            var sequences = new[] {new Sequence(11), new Sequence(4), new Sequence(13)};

            Assert.AreEqual(4L, Util.GetMinimumSequence(sequences));
        }

        [Test]
        public void ShouldReturnLongMaxWhenNoEventProcessors()
        {
            var sequences = new Sequence[0];

            Assert.AreEqual(long.MaxValue, Util.GetMinimumSequence(sequences));
        }

        [Test]
        public void ShouldReadObjectFromArray()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new StubEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.Read<StubEvent>(array, i);

                Assert.AreEqual(new StubEvent(i), evt);
            }
        }

        [Test]
        public void ShouldReadValueFromArray()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new StubValueEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<StubValueEvent>(array, i);

                Assert.AreEqual(new StubValueEvent(i), evt);
            }
        }

        [Test]
        public void ShouldReadValueAtIndexUnaligned()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new UnalignedEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<UnalignedEvent>(array, i);

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
                    var evt = Util.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);

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
                ref var evt = ref Util.ReadValue<StubValueEvent>(array, i);
                evt.TestString = evt.Value.ToString();
            }

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<StubValueEvent>(array, i);

                Assert.AreEqual(evt.Value.ToString(), evt.TestString);
            }
        }

        [Test]
        public void ShouldMutateValueFromArrayUnaligned()
        {
            var array = Enumerable.Range(0, 2000).Select(x => new UnalignedEvent(x)).ToArray();

            for (var i = 0; i < array.Length; i++)
            {
                ref var evt = ref Util.ReadValue<UnalignedEvent>(array, i);
                evt.TestString = evt.Value.ToString();
            }

            for (var i = 0; i < array.Length; i++)
            {
                var evt = Util.ReadValue<UnalignedEvent>(array, i);

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
                    ref var evt = ref Util.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);
                    evt.DoubleValue = evt.Value + 0.1;
                }

                for (var i = 0; i < memory.EventCount; i++)
                {
                    var evt = Util.ReadValue<StubUnmanagedEvent>(memory.PointerToFirstEvent, i, memory.EventSize);

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
            public string TestString;

            [FieldOffset(11)]
            public int Value;
        }
    }
}
