using Disruptor.Internal;
using NUnit.Framework;

namespace Disruptor.Tests.Internal
{
    [TestFixture]
    public class StructProxyTests
    {
        [Test]
        public void ShouldGenerateProxyForType()
        {
            var foo = new Foo();
            var fooProxy = StructProxy.CreateProxyInstance<IFoo>(foo);

            Assert.IsNotNull(fooProxy);
            Assert.IsTrue(fooProxy.GetType().IsValueType);
            Assert.IsInstanceOf<IFoo>(fooProxy);
            Assert.IsInstanceOf<IOtherInterface>(fooProxy);

            fooProxy.Value = 888;

            Assert.AreEqual(foo.Value, 888);
            Assert.AreEqual(fooProxy.Value, 888);

            fooProxy.Compute(400, 44);

            Assert.AreEqual(foo.Value, 444);
            Assert.AreEqual(fooProxy.Value, 444);
        }

        [Test]
        public void ShouldNotFailForInternalType()
        {
            var foo = new InternalFoo();
            IFoo fooProxy = null;

            Assert.DoesNotThrow(() => fooProxy = StructProxy.CreateProxyInstance<IFoo>(foo));

            Assert.IsNotNull(fooProxy);
            Assert.AreEqual(fooProxy, foo);
        }

        [Test]
        public void ShouldNotFailForPublicTypeWithInternalGenericArgument()
        {
            var bar = new Bar<InternalBarArg>();
            IBar<InternalBarArg> barProxy = null;

            Assert.DoesNotThrow(() => barProxy = StructProxy.CreateProxyInstance<IBar<InternalBarArg>>(bar));

            Assert.IsNotNull(barProxy);
            Assert.AreEqual(barProxy, bar);
        }

        public interface IFoo
        {
            int Value { get; set; }

            void Compute(int a, long b);
        }

        public interface IOtherInterface
        {
        }

        public class Foo : IFoo, IOtherInterface
        {
            public int Value { get; set; }

            public void Compute(int a, long b)
            {
                Value = (int)(a + b);
            }
        }

        internal class InternalFoo : IFoo
        {
            public int Value { get; set; }

            public void Compute(int a, long b)
            {
            }
        }

        public interface IBar<T>
        {
        }

        public class Bar<T> : IBar<T>
        {

        }

        internal class InternalBarArg
        {
        }
    }
}
