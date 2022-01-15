using System;
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

        var exceptionHandler = new IgnoreExceptionHandler<StubEvent>();
        exceptionHandler.HandleEventException(exception, 0L, stubEvent);
    }
}