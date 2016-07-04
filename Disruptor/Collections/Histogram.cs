using System;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Disruptor.Collections
{
    /// <summary>
    ///     Histogram for tracking the frequency of observations of values below interval upper bounds.
    ///     This class is useful for recording timings in nanoseconds across a large number of observations
    ///     when high performance is required.
    ///     The interval bounds are used to define the ranges of the histogram buckets. If provided bounds
    ///     are [10, 20, 30, 40, 50] then there will be five buckets, accessible by index 0-4. Any value
    ///     0-10 will fall into the first interval bar, values 11-20 will fall into the
    ///     second bar, and so on.
    /// </summary>
    public class Histogram
    {
        private readonly long[] _counts;
        private readonly long[] _upperBounds;

        /// Create a new Histogram with a provided list of interval bounds.
        /// <param name="upperBounds">
        ///     upperBounds of the intervals.Bounds
        ///     must be provided in order least to greatest, and lowest bound
        ///     must be greater than or equal to 1.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">if any of the upper bounds are less than or equal to zero</exception>
        /// <exception cref="ArgumentOutOfRangeException">if the bounds are not in order, least to greatest</exception>
        public Histogram(long[] upperBounds)
        {
            ValidateBounds(upperBounds);

            _upperBounds = new long[upperBounds.Length];

            Array.Copy(upperBounds, _upperBounds, upperBounds.Length);
            _counts = new long[upperBounds.Length];
        }

        /// <summary>
        /// Size of the list of interval bars (ie: count of interval bars).
        /// </summary>
        public int Size => _upperBounds.Length;

        /// <summary>
        /// Count total number of recorded observations.
        /// </summary>
        /// <returns>the total number of recorded observations.</returns>
        public long Count
        {
            get
            {
                var count = 0L;

                for (var i = 0; i < _counts.Length; i++)
                {
                    count += _counts[i];
                }

                return count;
            }
        }

        /// <summary>
        /// Get the minimum observed value.
        /// </summary>
        public long Min { get; private set; } = long.MaxValue;

        /// <summary>
        /// Get the maximum observed value.
        /// </summary>
        public long Max { get; private set; }

        /// <summary>
        /// Calculate the mean of all recorded observations.
        ///
        /// The mean is calculated by summing the mid points of each interval multiplied by the count
        /// for that interval, then dividing by the total count of observations.The max and min are
        /// considered for adjusting the top and bottom bin when calculating the mid point, this
        /// minimises skew if the observed values are very far away from the possible histogram values
        /// </summary>
        public decimal Mean
        {
            get
            {
                if (Count == 0)
                {
                    return 0;
                }

                var lowerBound = _counts[0] > 0L ? Min : 0L;
                decimal total = 0;

                for (var i = 0; i < _upperBounds.Length; i++)
                {
                    if (_counts[i] != 0L)
                    {
                        var upperBound = Math.Min(_upperBounds[i], Max);
                        var midPoint = lowerBound + ((upperBound - lowerBound) / 2L);

                        var intervalTotal = (decimal)midPoint * _counts[i];
                        total += intervalTotal;
                    }

                    lowerBound = Math.Max(_upperBounds[i] + 1L, Min);
                }

                return Math.Round(total / Count, 2, MidpointRounding.AwayFromZero);
            }
        }

        /// <summary>
        /// Calculate the upper bound within which 99% of observations fall.
        /// </summary>
        public long TwoNinesUpperBound => GetUpperBoundForFactor(0.99d);

        /// <summary>
        ///     Calculate the upper bound within which 99.99% of observations fall.
        /// </summary>
        public long FourNinesUpperBound => GetUpperBoundForFactor(0.9999d);

        private static void ValidateBounds(long[] upperBounds)
        {
            long lastBound = -1L;
            if (upperBounds.Length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(upperBounds), "Must provide at least one interval");
            }
            foreach (long bound in upperBounds)
            {
                if (bound <= 0L)
                {
                    throw new ArgumentOutOfRangeException(nameof(upperBounds), "Bounds must be positive values");
                }

                if (bound <= lastBound)
                {
                    throw new ArgumentOutOfRangeException(nameof(upperBounds), "bound " + bound + " is not greater than " + lastBound);
                }

                lastBound = bound;
            }
        }

        /// <summary>
        ///     Get the upper bound of an interval for an index.
        /// </summary>
        /// <param name="index">index of the upper bound.</param>
        /// <returns>the interval upper bound for the index.</returns>
        public long GetUpperBoundAt(int index) => _upperBounds[index];

        /// <summary>
        ///     Get the count of observations at a given index.
        /// </summary>
        /// <param name="index">index of the observations counter.</param>
        /// <returns>the count of observations at a given index.</returns>
        public long GetCountAt(int index) => _counts[index];

        /// <summary>
        ///     Add an observation to the histogram and increment the counter for the interval it matches.
        /// </summary>
        /// <param name="value">value for the observation to be added.</param>
        /// <returns>return true if in the range of intervals otherwise false.</returns>
        public bool AddObservation(long value)
        {
            int low = 0;
            int high = _upperBounds.Length - 1;

            while (low < high)
            {
                int mid = low + ((high - low) >> 1);
                if (_upperBounds[mid] < value)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            if (value <= _upperBounds[high])
            {
                _counts[high]++;
                TrackRange(value);

                return true;
            }

            return false;
        }

        private void TrackRange(long value)
        {
            if (value < Min)
            {
                Min = value;
            }
            if (value > Max)
            {
                Max = value;
            }
        }

        /// <summary>
        ///     Add observations from another Histogram into this one.
        ///     Histograms must have the same intervals.
        /// </summary>
        /// <param name="histogram">histogram from which to add the observation counts.</param>
        /// <exception cref="ArgumentException">if interval count or values do not match exactly</exception>
        public void AddObservations(Histogram histogram)
        {
            if (_upperBounds.Length != histogram._upperBounds.Length)
            {
                throw new ArgumentException("Histograms must have matching intervals", nameof(histogram));
            }

            for (int i = 0; i < _upperBounds.Length; i++)
            {
                if (_upperBounds[i] != histogram._upperBounds[i])
                {
                    throw new ArgumentException("Histograms must have matching intervals", nameof(histogram));
                }
            }

            for (int i = 0; i < _counts.Length; i++)
            {
                _counts[i] += histogram._counts[i];
            }

            TrackRange(histogram.Min);
            TrackRange(histogram.Max);
        }

        /// <summary>
        /// Clear the list of interval counters.
        /// </summary>
        public void Clear()
        {
            Max = 0L;
            Min = long.MaxValue;

            for (int i = 0; i < _counts.Length; i++)
            {
                _counts[i] = 0L;
            }
        }

        /// <summary>
        ///     Get the interval upper bound for a given factor of the observation population.
        /// </summary>
        /// <param name="factor">factor representing the size of the population.</param>
        /// <returns>the interval upper bound.</returns>
        public long GetUpperBoundForFactor(double factor)
        {
            if (factor < 0 || factor > 1)
            {
                throw new ArgumentException("factor must be > 0.0 and < 1.0", nameof(factor));
            }

            var totalCount = Count;
            var tailTotal = (long)(totalCount - Math.Round(totalCount * factor));
            var tailCount = 0L;

            for (var i = _counts.Length - 1; i >= 0; i--)
            {
                if (_counts[i] != 0L)
                {
                    tailCount += _counts[i];
                    if (tailCount >= tailTotal)
                    {
                        return _upperBounds[i];
                    }
                }
            }

            return 0L;
        }

        /// <summary>
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            var culture = Thread.CurrentThread.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;

                var sb = new StringBuilder();

                sb.Append("Histogram{");

                sb.Append("min=").Append(Min).Append(", ");
                sb.Append("max=").Append(Max).Append(", ");
                sb.Append("mean=").Append(Mean).Append(", ");
                sb.Append("99%=").Append(TwoNinesUpperBound).Append(", ");
                sb.Append("99.99%=").Append(FourNinesUpperBound).Append(", ");

                sb.Append('[');
                for (var i = 0; i < _counts.Length; i++)
                {
                    sb.Append(_upperBounds[i]).Append('=').Append(_counts[i]).Append(", ");
                }

                if (_counts.Length > 0)
                {
                    sb.Length = (sb.Length - 2);
                }
                sb.Append(']');

                sb.Append('}');

                return sb.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }
    }
}
