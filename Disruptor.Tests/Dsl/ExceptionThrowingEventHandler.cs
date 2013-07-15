using System;
using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl
{
    public class ExceptionThrowingEventHandler : IEventHandler<TestEvent>
    {
        private readonly Exception _testException;
        public ExceptionThrowingEventHandler(Exception testException) { _testException = testException; }
        public void OnNext(TestEvent data, long sequence, bool endOfBatch) { throw _testException; }
    }
}