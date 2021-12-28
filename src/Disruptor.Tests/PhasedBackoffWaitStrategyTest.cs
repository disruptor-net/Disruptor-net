using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Tests.Support;
using JetBrains.Annotations;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public abstract class PhasedBackoffWaitStrategyTest : WaitStrategyFixture<PhasedBackoffWaitStrategy>
    {
        protected PhasedBackoffWaitStrategyTest(PhasedBackoffWaitStrategy waitStrategy)
            : base(waitStrategy)
        {
        }

        public class WithLock : PhasedBackoffWaitStrategyTest
        {
            public WithLock()
                : base(PhasedBackoffWaitStrategy.WithLock(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)))
            {
            }
        }

        public class WithSleep : PhasedBackoffWaitStrategyTest
        {
            public WithSleep()
                : base(PhasedBackoffWaitStrategy.WithSleep(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(1)))
            {
            }
        }
    }
}
