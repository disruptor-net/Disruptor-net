using System;
using System.IO;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class IgnoreExceptionHandlerTests
{
    [Test]
    public void ShouldIgnoreException()
    {
        var exception = new Exception();
        var stubEvent = new StubEvent(0);
        var log = new StringWriter();
        var exceptionHandler = new IgnoreExceptionHandler<StubEvent>(log);

        Assert.DoesNotThrow(() => exceptionHandler.HandleEventException(exception, 123, stubEvent));

        var logText = log.ToString();
        Assert.That(logText, Contains.Substring($"Exception processing sequence {123}"));
    }
}
