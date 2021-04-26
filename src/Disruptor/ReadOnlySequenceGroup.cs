using System;
using System.Linq;

namespace Disruptor
{
    /// <summary>
    /// Hides a group of Sequences behind a single Sequence
    /// </summary>
    internal sealed class ReadOnlySequenceGroup : ISequence
    {
        private readonly ISequence[] _sequences;

        /// <summary> </summary>
        /// <param name="sequences">sequences the list of sequences to be tracked under this sequence group</param>
        public ReadOnlySequenceGroup(ISequence[] sequences)
        {
            _sequences = sequences.ToArray();
        }

        /// <summary>
        /// Get the minimum sequence value for the group.
        /// </summary>
        public long Value => DisruptorUtil.GetMinimumSequence(_sequences);

        /// <summary>
        /// Not supported.
        /// </summary>
        public void SetValue(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported
        /// </summary>
        /// <param name="value"></param>
        public void SetValueVolatile(long value)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public bool CompareAndSet(long expectedValue, long newValue)
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public long IncrementAndGet()
        {
            throw new NotSupportedException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        public long AddAndGet(long increment)
        {
            throw new NotSupportedException();
        }

        public override string ToString()
        {
            return $"FixedSequenceGroup {{{string.Join(", ", _sequences.Select(x => x.ToString()))}}}";
        }
    }
}
