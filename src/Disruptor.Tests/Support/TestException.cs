using System;

namespace Disruptor.Tests.Support;

public class TestException
{
    public static Action ThrowOnce()
    {
        var thrown = false;

        return () =>
        {
            if (!thrown)
            {
                thrown = true;
                throw new Exception("Test");
            }
        };
    }
}
