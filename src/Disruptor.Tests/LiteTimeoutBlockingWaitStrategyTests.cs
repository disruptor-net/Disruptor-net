using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class LiteTimeoutBlockingWaitStrategyTests : WaitStrategyFixture<LiteTimeoutBlockingWaitStrategy>
    {
        public LiteTimeoutBlockingWaitStrategyTests()
            : base(new LiteTimeoutBlockingWaitStrategy(TimeSpan.FromSeconds(30)))
        {
        }

        [Test]
        public void ShouldTimeoutWaitFor()
        {
            var theTimeout = TimeSpan.FromMilliseconds(500);
            var waitStrategy = new LiteTimeoutBlockingWaitStrategy(theTimeout);
            var cursor = new Sequence(5);
            var dependent = cursor;

            var stopwatch = Stopwatch.StartNew();

            var waitResult = waitStrategy.WaitFor(6, cursor, dependent, default);

            stopwatch.Stop();

            Assert.AreEqual(SequenceWaitResult.Timeout, waitResult);

            // Required to make the test pass on azure pipelines.
            var tolerance = TimeSpan.FromMilliseconds(25);

            Assert.That(stopwatch.Elapsed, Is.GreaterThanOrEqualTo(theTimeout - tolerance));
        }
    }
}
