using System.Threading;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatileReferenceTests
    {
        private Volatile.Reference<RefStub> _volatile;
        private readonly RefStub _initialValue = new RefStub();
        private readonly RefStub _newValue = new RefStub();

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.Reference<RefStub>(_initialValue);
        }

        [Test]
        public void ReadAcquireFenceReturnsInitialValue()
        {
            Assert.AreEqual(_initialValue, _volatile.ReadAcquireFence());
        }

        [Test]
        public void ReadFullFenceReturnsInitialValue()
        {
            Assert.AreEqual(_initialValue, _volatile.ReadFullFence());
        }

        [Test]
        public void ReadCompilerOnlyFenceReturnsInitialValue()
        {
            Assert.AreEqual(_initialValue, _volatile.ReadCompilerOnlyFence());
        }

        [Test]
        public void ReadUnfencedReturnsInitialValue()
        {
            Assert.AreEqual(_initialValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteReleaseFenceChangesInitialValue()
        {
            _volatile.WriteReleaseFence(_newValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteFullFenceChangesInitialValue()
        {
            _volatile.WriteFullFence(_newValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteCompilerOnlyFenceChangesInitialValue()
        {
            _volatile.WriteCompilerOnlyFence(_newValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void WriteUnfencedInitialValue()
        {
            _volatile.WriteUnfenced(_newValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicCompareExchangeReturnsTrueIfComparandEqualsCurrentValue()
        {
            Assert.IsTrue(_volatile.AtomicCompareExchange(_newValue, _initialValue));
        }

        [Test]
        public void AtomicCompareExchangeMutatesValueIfComparandEqualsCurrentValue()
        {
            _volatile.AtomicCompareExchange(_newValue, _initialValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void AtomicCompareExchangeReturnsFalseIfComparandDifferentFromCurrentValue()
        {
            Assert.IsFalse(_volatile.AtomicCompareExchange(_newValue, _newValue));
        }

        [Test]
        public void AtomicExchangeReturnsInitialValue()
        {
            Assert.AreEqual(_initialValue, _volatile.AtomicExchange(_newValue));
        }

        [Test]
        public void AtomicExchangeMutatesValue()
        {
            _volatile.AtomicExchange(_newValue);
            Assert.AreEqual(_newValue, _volatile.ReadUnfenced());
        }

        [Test]
        public void ToStringReturnsInitialValueAsString()
        {
            Assert.AreEqual(_initialValue.ToString(), _volatile.ToString());
        }
    }

    public class RefStub{}
}