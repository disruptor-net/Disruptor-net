using System;
using System.Linq;

namespace Disruptor
{
    /// <summary>
    /// Hides a group of Sequences behind a single Sequence
    /// </summary>
    internal sealed class FixedSequenceGroup : Sequence
    {
        private readonly Sequence[] _sequences;

        /// <summary> </summary>
        /// <param name="sequences">sequences the list of sequences to be tracked under this sequence group</param>
        public FixedSequenceGroup(Sequence[] sequences)
        {
            _sequences = sequences.ToArray();
        }

        /// <summary>
        /// Get the minimum sequence value for the group.
        /// </summary>
        public override long Value => Util.GetMinimumSequence(_sequences);

        /// <summary>
        /// Not supported.
        /// </summary>
        public override void SetValue(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public override bool CompareAndSet(long expectedValue, long newValue)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public override long IncrementAndGet()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public override long AddAndGet(long increment)
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return string.Join(", ", _sequences.Select(x => x.ToString()));
        }
    }
}