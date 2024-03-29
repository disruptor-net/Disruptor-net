﻿using System;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs;

public class ExceptionThrowingEventHandler : IEventHandler<TestEvent>, IValueEventHandler<TestValueEvent>
{
    private readonly Exception _applicationException;

    public ExceptionThrowingEventHandler(Exception applicationException)
    {
        _applicationException = applicationException;
    }

    public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
    {
        throw _applicationException;
    }

    public void OnEvent(ref TestValueEvent data, long sequence, bool endOfBatch)
    {
        throw _applicationException;
    }
}