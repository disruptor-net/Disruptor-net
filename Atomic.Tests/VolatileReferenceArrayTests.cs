using System.Threading;
using Atomic.Tests.Support;
using NUnit.Framework;

namespace Atomic.Tests
{
    [TestFixture]
    public class VolatileReferenceArrayTests
    {
        private Volatile.ReferenceArray<ClassStub> _volatile;
        private static readonly ClassStub InitialValue = new ClassStub();
        private static readonly ClassStub NewValue = new ClassStub();
        private static readonly ClassStub[] InitialValues = new[] { InitialValue };

        [SetUp]
        public void SetUp()
        {
            _volatile = new Volatile.ReferenceArray<ClassStub>(InitialValues);
        }

        [Test]
        public void ConstructorWithLengthSetExpectedLength()
        {
            const int length = 2;
            _volatile = new Volatile.ReferenceArray<ClassStub>(length);

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