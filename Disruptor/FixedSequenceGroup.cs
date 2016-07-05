using System;
using System.Linq;

namespace Disruptor
{
    internal sealed class FixedSequenceGroup : Sequence
    {
        private readonly Sequence[] _sequences;

        /// <summary> </summary>
        /// <param name="sequences">sequences the list of sequences to be tracked under this sequence group</param>
        public FixedSequenceGroup(Sequence[] sequences)
        {
            _sequences = new Sequence[sequences.Length];
            sequences.CopyTo(_sequences, 0);
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="expectedValue"></param>
        /// <param name="newValue"></param>
        /// <returns></returns>
        public override bool CompareAndSet(long expectedValue, long newValue)
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <returns></returns>
        public override long IncrementAndGet()
        {
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Not supported.
        /// </summary>
        /// <param name="increment"></param>
        /// <returns></returns>
        public override long AddAndGet(long increment)
        {
            throw new InvalidOperationException();
        }

        public override string ToString()
        {
            return string.Join(", ", _sequences.Select(x => x.ToString()));
        }
    }
}