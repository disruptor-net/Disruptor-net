using System;
using System.IO;
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
        var log = new StringWriter();

        var exceptionHandler = new FatalExceptionHandler<StubEvent>(log);

        var exception = Assert.Throws<ApplicationException>(() => exceptionHandler.HandleEventException(causeException, 123, evt));

        Assert.That(exception, Is.Not.Null);
        Assert.That(exception!.InnerException, Is.EqualTo(causeException));

        var logText = log.ToString();
        Assert.That(logText, Contains.Substring($"Exception processing sequence {123}"));
    }
}
