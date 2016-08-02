using System;

namespace Disruptor
{
    class TimeoutException : ApplicationException
    {
        public static readonly TimeoutException Instance = new TimeoutException();

        private TimeoutException()
        {
        }
    }
}