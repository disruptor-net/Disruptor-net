using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class FatalExceptionHandlerTests
{
    [Test]
    public void ShouldHandleFatalException()
    {
        var causeException = new Exception();
        var evt = new StubEvent(0);

        var exceptionHandler = new FatalExceptionHandler<StubEvent>();

        var exception = Assert.Throws<ApplicationException>(() => exceptionHandler.HandleEventException(causeException, 0L, evt));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.InnerException, Is.EqualTo(causeException));
    }
}
