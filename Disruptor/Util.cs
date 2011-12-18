namespace Disruptor
{
    /// <summary>
    /// Set of common functions used by the Disruptor
    /// </summary>
    internal static class Util
    {
        /// <summary>
        /// Calculate the next power of 2, greater than or equal to x.
        /// </summary>
        /// <param name="x">Value to round up</param>
        /// <returns>The next power of 2 from x inclusive</returns>
        public static int CeilingNextPowerOfTwo(int x)
        {
            var result = 2;

            while (result < x)
            {
                result *= 2;
            }

            return result;
        }

        /// <summary>
        /// Get the minimum sequence from an array of <see cref="Sequence"/>s.
        /// </summary>
        /// <param name="sequences">sequences to compare.</param>
        /// <returns>the minimum sequence found or lon.MaxValue if the array is empty.</returns>
        public static long GetMinimumSequence(Sequence[] sequences)
        {
            if (sequences.Length == 0) return long.MaxValue;

            var min = long.MaxValue;
            for (var i = 0; i < sequences.Length; i++)
            {
                var sequence = sequences[i].Value; // volatile read
                min = min < sequence ? min : sequence;
            }
            return min;
        }

        /// <summary>
        /// Get an array of <see cref="Sequence"/>s for the passed <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="processors">processors for which to get the sequences</param>
        /// <returns>the array of <see cref="Sequence"/>s</returns>
        public static Sequence[] GetSequencesFor(params IEventProcessor[] processors)
        {
            var sequences = new Sequence[processors.Length];
            for (int i = 0; i < sequences.Length; i++)
            {
                sequences[i] = processors[i].Sequence;
            }

            return sequences;
        }

        
    }
}