using System;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class LiteTimeoutBlockingWaitStrategyTests
    {
        [Test]
        public void ShouldTimeoutWaitFor()
        {
            var sequenceBarrier = new DummySequenceBarrier();

            var theTimeout = TimeSpan.FromMilliseconds(500);
            var waitStrategy = new LiteTimeoutBlockingWaitStrategy(theTimeout);
            Sequence cursor = new Sequence(5);
            Sequence dependent = cursor;

            var t0 = DateTime.UtcNow;

            try
            {
                waitStrategy.WaitFor(6, cursor, dependent, sequenceBarrier);
                Assert.Fail("TimeoutException should have been thrown");
            }
            catch (TimeoutException)
            {
            }

            var t1 = DateTime.UtcNow;

            var timeWaiting = t1 - t0;

            Assert.That(timeWaiting, Is.GreaterThanOrEqualTo(theTimeout));
        }
    }
}