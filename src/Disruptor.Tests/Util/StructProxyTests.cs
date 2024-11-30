using Disruptor.Util;
using NUnit.Framework;

namespace Disruptor.Tests.Util;

[TestFixture]
public class StructProxyTests
{
    [Test]
    public void ShouldGenerateProxyForType()
    {
        var foo = new Foo();
        var fooProxy = StructProxy.CreateProxyInstance<IFoo>(foo);

        Assert.That(fooProxy, Is.Not.Null);
        Assert.That(fooProxy.GetType().IsValueType);
        Assert.That(fooProxy, Is.InstanceOf<IFoo>());
        Assert.That(fooProxy, Is.InstanceOf<IOtherInterface>());

        fooProxy.Value = 888;

        Assert.That(888, Is.EqualTo(foo.Value));
        Assert.That(888, Is.EqualTo(fooProxy.Value));

        fooProxy.Compute(400, 44);

        Assert.That(444, Is.EqualTo(foo.Value));
        Assert.That(444, Is.EqualTo(fooProxy.Value));
    }

    [Test]
    public void ShouldNotFailForInternalType()
    {
        var foo = new InternalFoo();
        var fooProxy = StructProxy.CreateProxyInstance<IFoo>(foo);

        Assert.DoesNotThrow(() => fooProxy.Value = 1);
        Assert.DoesNotThrow(() => fooProxy.Compute(1, 2));

        Assert.That(fooProxy, Is.Not.Null);
        Assert.That((IFoo?)foo, Is.SameAs(fooProxy));
    }

    [Test]
    public void ShouldNotFailForExplicitImplementation()
    {
        var foo = new ExplicitImplementation();
        var fooProxy = StructProxy.CreateProxyInstance<IFoo>(foo);

        Assert.DoesNotThrow(() => fooProxy.Value = 1);
        Assert.DoesNotThrow(() => fooProxy.Compute(1, 2));

        Assert.That(fooProxy, Is.Not.Null);
        Assert.That((IFoo?)foo, Is.SameAs(fooProxy));
    }

    [Test]
    public void ShouldNotFailForPublicTypeWithInternalGenericArgument()
    {
        var bar = new Bar<InternalBarArg>();
        var barProxy = StructProxy.CreateProxyInstance<IBar<InternalBarArg>>(bar);

        Assert.That(barProxy, Is.Not.Null);
        Assert.That((IBar<InternalBarArg>?)bar, Is.EqualTo(barProxy));
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

    public class ExplicitImplementation : IFoo
    {
        int IFoo.Value { get; set; }

        void IFoo.Compute(int a, long b)
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
