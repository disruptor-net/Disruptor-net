using System.Threading;
using Atomic.Tests.Support;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatileReferenceTests
    {
        private Volatile.Reference<ClassStub> _volatile;
        private readonly ClassStub _initialValue = new ClassStub();
        private readonly ClassStub _newValue = new ClassStub();

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.Reference<ClassStub>(_initialValue);
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
}