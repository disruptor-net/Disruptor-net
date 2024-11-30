using System.Linq;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class UnmanagedRingBufferMemoryTests
{
    [Test]
    public unsafe void ShouldCreateMemoryFromSize()
    {
        using (var memory = UnmanagedRingBufferMemory.Allocate(1024, 8))
        {
            Assert.That(memory.EventCount, Is.EqualTo(1024));
            Assert.That(memory.EventSize, Is.EqualTo(8));

            var pointer = (Data*)memory.PointerToFirstEvent;

            for (var i = 0; i < memory.EventCount; i++)
            {
                var data = pointer[i];
                Assert.That(data.Value1, Is.EqualTo(0));
                Assert.That(data.Value2, Is.EqualTo(0));
            }
        }
    }

    [Test]
    public unsafe void ShouldCreateMemoryFromFactory()
    {
        var index = 0;
        using (var memory = UnmanagedRingBufferMemory.Allocate(1024, () => new Data { Value2 = index++ }))
        {
            Assert.That(memory.EventCount, Is.EqualTo(1024));
            Assert.That(memory.EventSize, Is.EqualTo(8));

            var pointer = (Data*)memory.PointerToFirstEvent;

            for (var i = 0; i < memory.EventCount; i++)
            {
                var data = pointer[i];
                Assert.That(data.Value1, Is.EqualTo(0));
                Assert.That(data.Value2, Is.EqualTo(i));
            }
        }
    }

    [Test]
    public void ShouldConvertMemoryToArray()
    {
        var index = 0;
        using (var memory = UnmanagedRingBufferMemory.Allocate(32, () => new Data { Value1 = index++ }))
        {
            var array = memory.ToArray<Data>();

            var expectedArray = Enumerable.Range(0, 32).Select(i => new Data { Value1 = i }).ToArray();
            Assert.That(array, Is.EqualTo(expectedArray));
        }
    }

    private struct Data
    {
        public int Value1;
        public int Value2;
    }
}
