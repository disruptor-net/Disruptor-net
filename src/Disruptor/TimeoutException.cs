using System;

namespace Disruptor
{
    class TimeoutException : AggregateException
    {
        public static readonly TimeoutException Instance = new TimeoutException();

        private TimeoutException()
        {
        }
    }
}