using System.Threading;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatileBooleanArrayTests
    {
        private Volatile.BooleanArray _volatile;
        private static readonly bool[] InitialValues = new[] {true, false, true, false};
        private const bool InitialValue = true;
        private const bool NewValue = false;

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.BooleanArray(InitialValues);
        }

        [Test]
        public void ConstructorWithLengthSetExpectedLength()
        {
            const int length = 2;
            _volatile = new Volatile.BooleanArray(length);

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
            Assert.IsFalse(_volatile.AtomicCompareExchange(0, NewValue, NewValue));
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
    }
}