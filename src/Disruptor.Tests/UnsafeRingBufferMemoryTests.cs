using System.Linq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class UnsafeRingBufferMemoryTests
    {
        [Test]
        public unsafe void ShouldCreateMemoryFromSize()
        {
            using (var memory = UnsafeRingBufferMemory.Allocate(1024, 8))
            {
                Assert.AreEqual(1024, memory.EventCount);
                Assert.AreEqual(8, memory.EventSize);

                var pointer = (Data*)memory.PointerToFirstEvent;

                for (var i = 0; i < memory.EventCount; i++)
                {
                    var data = pointer[i];
                    Assert.AreEqual(0, data.Value1);
                    Assert.AreEqual(0, data.Value2);
                }
            }
        }

        [Test]
        public unsafe void ShouldCreateMemoryFromFactory()
        {
            var index = 0;
            using (var memory = UnsafeRingBufferMemory.Allocate(1024, () => new Data { Value2 = index++ }))
            {
                Assert.AreEqual(1024, memory.EventCount);
                Assert.AreEqual(8, memory.EventSize);

                var pointer = (Data*)memory.PointerToFirstEvent;

                for (var i = 0; i < memory.EventCount; i++)
                {
                    var data = pointer[i];
                    Assert.AreEqual(0, data.Value1);
                    Assert.AreEqual(i, data.Value2);
                }
            }
        }

        [Test]
        public void ShouldConvertMemoryToArray()
        {
            var index = 0;
            using (var memory = UnsafeRingBufferMemory.Allocate(32, () => new Data { Value1 = index++ }))
            {
                var array = memory.ToArray<Data>();

                var expectedArray = Enumerable.Range(0, 32).Select(i => new Data { Value1 = i }).ToArray();
                Assert.AreEqual(expectedArray, array);
            }
        }

        private struct Data
        {
            public int Value1;
            public int Value2;
        }
    }
}
