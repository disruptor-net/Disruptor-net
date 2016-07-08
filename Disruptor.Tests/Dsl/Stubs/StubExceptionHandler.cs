﻿using System;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class StubExceptionHandler : IExceptionHandler<object>
    {
        private readonly AtomicReference<Exception> _exceptionHandled;

        public StubExceptionHandler(AtomicReference<Exception> exceptionHandled)
        {
            _exceptionHandled = exceptionHandled;
        }

        public void HandleEventException(Exception ex, long sequence, object @event)
        {
            _exceptionHandled.Write(ex);
        }

        public void HandleOnStartException(Exception ex)
        {
            _exceptionHandled.Write(ex);
        }

        public void HandleOnShutdownException(Exception ex)
        {
            _exceptionHandled.Write(ex);
        }
    }
}