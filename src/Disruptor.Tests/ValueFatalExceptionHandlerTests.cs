using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class ValueFatalExceptionHandlerTests
{
    [Test]
    public void ShouldHandleFatalException()
    {
        var causeException = new Exception();
        var evt = new StubValueEvent(0);

        var exceptionHandler = new ValueFatalExceptionHandler<StubValueEvent>();

        try
        {
            exceptionHandler.HandleEventException(causeException, 0L, ref evt);
        }
        catch (Exception ex)
        {
            Assert.AreEqual(causeException, ex.InnerException);
        }
    }
}