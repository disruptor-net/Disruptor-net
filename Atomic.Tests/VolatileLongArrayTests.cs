using System.Threading;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatileLongArrayTests
    {
        private Volatile.LongArray _volatile;
        private static readonly long[] InitialValues = new[] {0L, 1L, 2L, 3L};
        private const long InitialValue = 0L;
        private const long NewValue = 1L;

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.LongArray(InitialValues);
        }

        [Test]
        public void ConstructorWithLengthSetExpectedLength()
        {
            const int length = 2;
            _volatile = new Volatile.LongArray(length);

            Assert.AreEqual(length, _volatile.Length);
        }

        [Test]
        public void ConstructorWithArraySetExpectedLength()
        {
            Assert.AreEqual(InitialValues.Length, _volatile.Length);
        }

        [Test]
        public void ReadFullFenceReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadFullFence(0));
        }

        [Test]
        public void ReadCompilerOnlyFenceReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadCompilerOnlyFence(0));
        }

        [Test]
        public void ReadUnfencedReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void WriteReleaseFenceChangesInitialValue()
        {
            _volatile.WriteReleaseFence(0, NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void WriteFullFenceChangesInitialValue()
        {
            _volatile.WriteFullFence(0, NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void WriteCompilerOnlyFenceChangesInitialValue()
        {
            _volatile.WriteCompilerOnlyFence(0, NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void WriteUnfencedInitialValue()
        {
            _volatile.WriteUnfenced(0, NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void AtomicCompareExchangeReturnsTrueIfComparandEqualsCurrentValue()
        {
            Assert.IsTrue(_volatile.AtomicCompareExchange(0, NewValue, InitialValue));
        }

        [Test]
        public void AtomicCompareExchangeMutatesValueIfComparandEqualsCurrentValue()
        {
            _volatile.AtomicCompareExchange(0, NewValue, InitialValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void AtomicCompareExchangeReturnsFalseIfComparandDifferentFromCurrentValue()
        {
            Assert.IsFalse(_volatile.AtomicCompareExchange(0, NewValue, InitialValue + 1));
        }

        [Test]
        public void AtomicExchangeReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.AtomicExchange(0, NewValue));
        }

        [Test]
        public void AtomicExchangeMutatesValue()
        {
            _volatile.AtomicExchange(0, NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void AtomicAddAndGetReturnsNewValue()
        {
            const int delta = 5;
            Assert.AreEqual(InitialValue + delta, _volatile.AtomicAddAndGet(0, delta));
        }

        [Test]
        public void AtomicAddAndGetMutatesValue()
        {
            const int delta = 5;
            _volatile.AtomicAddAndGet(0, delta);
            Assert.AreEqual(InitialValue + delta, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void AtomicIncrementAndGetReturnsNewValue()
        {
            Assert.AreEqual(InitialValue + 1, _volatile.AtomicIncrementAndGet(0));
        }

        [Test]
        public void AtomicIncrementAndGetMutatesValue()
        {
            _volatile.AtomicIncrementAndGet(0);
            Assert.AreEqual(InitialValue + 1, _volatile.ReadUnfenced(0));
        }

        [Test]
        public void AtomicDecrementAndGetReturnsNewValue()
        {
            Assert.AreEqual(InitialValue - 1, _volatile.AtomicDecrementAndGet(0));
        }

        [Test]
        public void AtomicDecrementAndGetMutatesValue()
        {
            _volatile.AtomicDecrementAndGet(0);
            Assert.AreEqual(InitialValue - 1, _volatile.ReadUnfenced(0));
        }
    }
}