using System;

namespace Disruptor
{
#if NETSTANDARD2_0
    class TimeoutException : AggregateException
#else
    class TimeoutException : Exception
#endif
    {
        public static readonly TimeoutException Instance = new TimeoutException();

        private TimeoutException()
        {
        }
    }
}