using System;
using System.Diagnostics;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class TimeoutBlockingWaitStrategyTests
    {
        [Test]
        public void ShouldTimeoutWaitFor()
        {
            var sequenceBarrier = new DummySequenceBarrier();

            var theTimeout = TimeSpan.FromMilliseconds(500);
            var waitStrategy = new TimeoutBlockingWaitStrategy(theTimeout);
            var cursor = new Sequence(5);
            var dependent = cursor;

            var stopwatch = Stopwatch.StartNew();

            Assert.Throws<TimeoutException>(() => waitStrategy.WaitFor(6, cursor, dependent, sequenceBarrier));

            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(theTimeout - TimeSpan.FromMilliseconds(5)));
        }
    }
}
