using System.Threading;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatilePaddedIntegerTests
    {
        private Volatile.PaddedInteger _volatile;
        private const int InitialValue = 2;
        private const int NewValue = 3;

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.PaddedInteger(InitialValue);
        }

        [Test]
        public void ReadAcquireFenceReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadAcquireFence());
        }

        [Test]
        public void ReadFullFenceReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadFullFence());
        }

        [Test]
        public void ReadCompilerOnlyFenceReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadCompilerOnlyFence());
        }

        [Test]
        public void ReadUnfencedReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteReleaseFenceChangesInitialValue()
        {
            _volatile.WriteReleaseFence(NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteFullFenceChangesInitialValue()
        {
            _volatile.WriteFullFence(NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteCompilerOnlyFenceChangesInitialValue()
        {
            _volatile.WriteCompilerOnlyFence(NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteUnfencedInitialValue()
        {
            _volatile.WriteUnfenced(NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicCompareExchangeReturnsTrueIfComparandEqualsCurrentValue()
        {
            Assert.IsTrue(_volatile.AtomicCompareExchange(NewValue, InitialValue));
        }

        [Test]
        public void AtomicCompareExchangeMutatesValueIfComparandEqualsCurrentValue()
        {
            _volatile.AtomicCompareExchange(NewValue, InitialValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicCompareExchangeReturnsFalseIfComparandDifferentFromCurrentValue()
        {
            Assert.IsFalse(_volatile.AtomicCompareExchange(NewValue, InitialValue + 1));
        }

        [Test]
        public void AtomicExchangeReturnsInitialValue()
        {
            Assert.AreEqual(InitialValue, _volatile.AtomicExchange(NewValue));
        }

        [Test]
        public void AtomicExchangeMutatesValue()
        {
            _volatile.AtomicExchange(NewValue);
            Assert.AreEqual(NewValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicAddAndGetReturnsNewValue()
        {
            const int delta = 5;
            Assert.AreEqual(InitialValue + delta, _volatile.AtomicAddAndGet(delta));
        }

        [Test]
        public void AtomicAddAndGetMutatesValue()
        {
            const int delta = 5;
            _volatile.AtomicAddAndGet(delta);
            Assert.AreEqual(InitialValue + delta, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicIncrementAndGetReturnsNewValue()
        {
            Assert.AreEqual(InitialValue + 1, _volatile.AtomicIncrementAndGet());
        }

        [Test]
        public void AtomicIncrementAndGetMutatesValue()
        {
            _volatile.AtomicIncrementAndGet();
            Assert.AreEqual(InitialValue + 1, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicDecrementAndGetReturnsNewValue()
        {
            Assert.AreEqual(InitialValue - 1, _volatile.AtomicDecrementAndGet());
        }

        [Test]
        public void AtomicDecrementAndGetMutatesValue()
        {
            _volatile.AtomicDecrementAndGet();
            Assert.AreEqual(InitialValue - 1, _volatile.ReadUnfenced());
        }

        [Test]
        public void ToStringReturnsInitialValueAsString()
        {
            Assert.AreEqual(InitialValue.ToString(), _volatile.ToString());
        }
    }
}