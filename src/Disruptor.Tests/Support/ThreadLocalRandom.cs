using System;
using System.Threading;

namespace Disruptor.Tests.Support
{
    public static class ThreadLocalRandom
    {
        private static readonly ThreadLocal<Random> _current = new ThreadLocal<Random>(() => new Random());

        public static Random Current => _current.Value;
    }
}
