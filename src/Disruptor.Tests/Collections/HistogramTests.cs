using System;
using System.Threading;
using Disruptor.Collections;
using NUnit.Framework;

namespace Disruptor.Tests.Collections
{
    [TestFixture]
    public class HistogramTests
    {
        private static readonly long[] _intervals = { 1, 10, 100, 1000, long.MaxValue};
        private Histogram _histogram;

        [SetUp]
        public void SetUp()
        {
            _histogram = new Histogram(_intervals);
        }

        [Test]
        public void ShouldSizeBasedOnBucketConfiguration()
        {
            Assert.AreEqual(_intervals.Length, _histogram.Size);
        }

        [Test]
        public void ShouldWalkIntervals()
        {
            for (var i = 0; i < _histogram.Size; i++)
            {
                Assert.AreEqual(_intervals[i], _histogram.GetUpperBoundAt(i));
            }
        }

        [Test]
        public void ShouldConfirmIntervalsAreInitialised()
        {
            for (var i = 0; i < _histogram.Size; i++)
            {
                Assert.AreEqual(0, _histogram.GetCountAt(i));
            }
        }

        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ShouldThrowExceptionWhenIntervalLessThanOrEqualToZero()
        {
            new Histogram(new long[]{-1, 10, 20});
        }

        [Test]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ShouldThrowExceptionWhenIntervalDoNotIncrease()
        {
            new Histogram(new long[]{1, 10, 10, 20});
        }

        [Test]
        public void ShouldAddObservation()
        {
            Assert.IsTrue(_histogram.AddObservation(10L));
            Assert.AreEqual(1L, _histogram.GetCount());
        }

        [Test]
        public void ShouldNotAddObservation()
        {
            var histogram = new Histogram(new long[]{ 10, 20, 30 });
            Assert.IsFalse(histogram.AddObservation(31L));
        }

        [Test]
        public void ShouldAddObservations()
        {
            AddObservations(_histogram, 10L, 30L, 50L);

            var histogram2 = new Histogram(_intervals);
            AddObservations(histogram2, 10L, 20L, 25L);

            _histogram.AddObservations(histogram2);

            Assert.AreEqual(6L, _histogram.GetCount());
        }

        [Test]
        [ExpectedException(typeof(ArgumentException))]
        public void ShouldThrowExceptionWhenIntervalsDoNotMatch()
        {
            var histogram2 = new Histogram(new[]{ 1L, 2L, 3L});
            _histogram.AddObservations(histogram2);
        }

        [Test]
        public void ShouldClearCounts()
        {
            AddObservations(_histogram, 1L, 7L, 10L, 3000L);
            _histogram.Clear();

            for (var i = 0; i < _histogram.Size; i++)
            {
                Assert.AreEqual(0L, _histogram.GetCountAt(i));
            }
        }

        [Test]
        public void ShouldCountTotalObservations()
        {
            AddObservations(_histogram, 1L, 7L, 10L, 3000L);

            Assert.AreEqual(4L, _histogram.GetCount());
        }

        [Test]
        public void ShouldGetMeanObservation()
        {
            var intervals = new long[]{ 1, 10, 100, 1000, 10000 };
            var histogram = new Histogram(intervals);

            AddObservations(histogram, 1L, 7L, 10L, 10L, 11L, 144L);

            Assert.AreEqual(32.67d, histogram.Mean);
        }

        [Test]
        public void ShouldCorrectMeanForSkewInTopAndBottomPopulatedIntervals()
        {
            var intervals = new long[]{ 100, 110, 120, 130, 140, 150, 1000, 10000 };
            var histogram = new Histogram(intervals);

            for (var i = 100; i < 152; i++)
            {
                histogram.AddObservation(i);
            }

            Assert.AreEqual(125.02d, histogram.Mean);
        }

        [Test]
        public void ShouldGetMaxObservation()
        {
            AddObservations(_histogram, 1L, 7L, 10L, 10L, 11L, 144L);

            Assert.AreEqual(144L, _histogram.Max);
        }

        [Test]
        public void ShouldGetMinObservation()
        {
            AddObservations(_histogram, 1L, 7L, 10L, 10L, 11L, 144L);

            Assert.AreEqual(1L, _histogram.Min);
        }

        [Test]
        public void ShouldGetMinAndMaxOfSingleObservation()
        {
            AddObservations(_histogram, 10L);
            Assert.AreEqual(10L, _histogram.Min);
            Assert.AreEqual(10L, _histogram.Max);
        }

        [Test]
        public void ShouldGetTwoNinesUpperBound()
        {
            var intervals = new long[]{ 1, 10, 100, 1000, 10000 };
            var histogram = new Histogram(intervals);

            for (long i = 1; i < 101; i++)
            {
                histogram.AddObservation(i);
            }

             Assert.AreEqual(100L, histogram.TwoNinesUpperBound);
        }

        [Test]
        public void ShouldGetFourNinesUpperBound()
        {
            var intervals = new long[]{ 1, 10, 100, 1000, 10000 };
            var histogram = new Histogram(intervals);

            for (long i = 1; i < 102; i++)
            {
                histogram.AddObservation(i);
            }

            Assert.AreEqual(1000L, histogram.FourNinesUpperBound);
        }

        [Test]
        public void ShouldToString()
        {
            AddObservations(_histogram, 1L, 7L, 10L, 300L);

            const string expectedResults = "Histogram{min=1, max=300, mean=53.25, 99%=1000, 99.99%=1000, [1=1, 10=2, 100=0, 1000=1, 9223372036854775807=0]}";
            Assert.AreEqual(expectedResults, _histogram.ToString());
        }

        private static void AddObservations(Histogram histogram, params long[] observations)
        {
            foreach (var observation in observations)
            {
                histogram.AddObservation(observation);
            }
        }
    }
}