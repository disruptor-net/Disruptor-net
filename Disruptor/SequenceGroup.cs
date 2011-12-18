using System;
using Disruptor.Atomic;

namespace Disruptor
{
    /// <summary>
    /// <see cref="Sequence"/> group that can dynamically have <see cref="Sequence"/>s added and removed while being
    /// thread safe.
    /// 
    /// The <see cref="SequenceGroup.Value"/> get and set methods are lock free and can be
    /// concurrently called with the <see cref="SequenceGroup.Add"/> and <see cref="SequenceGroup.Remove"/>.
    /// </summary>
    public class SequenceGroup : Sequence
    {
        private AtomicReference<Sequence[]> _sequencesRef = new AtomicReference<Sequence[]>(new Sequence[0]);

        /// <summary>
        /// Current sequence number
        /// </summary>
        public override long Value
        {
            get { return Util.GetMinimumSequence(_sequencesRef.Value); }
            set
            {
                var sequences = _sequencesRef.Value;
                for (int i = 0; i < sequences.Length; i++)
                {
                    sequences[i].Value = value;
                }
            }
        }

        /// <summary>
        /// Eventually sets to the given value.
        /// </summary>
        /// <param name="value">the new value</param>
        public override void LazySet(long value)
        {
            var sequences = _sequencesRef.Value;
            for (int i = 0; i < sequences.Length; i++)
            {
                sequences[i].LazySet(value);
            }
        }

        /// <summary>
        /// Add a <see cref="Sequence"/> into this aggregate.
        /// </summary>
        /// <param name="sequence">sequence to be added to the aggregate.</param>
        public void Add(Sequence sequence)
        {
            Sequence[] oldSequences;
            Sequence[] newSequences;
            do
            {
                oldSequences = _sequencesRef.Value;
                int oldSize = oldSequences.Length;
                newSequences = new Sequence[oldSize + 1];
                Array.Copy(oldSequences, newSequences, oldSize);
                newSequences[oldSize] = sequence;
            }
            while (!_sequencesRef.CompareAndSet(oldSequences, newSequences));
        }

        /// <summary>
        /// Remove the first occurrence of the <see cref="Sequence"/> from this aggregate.
        /// </summary>
        /// <param name="sequence">sequence to be removed from this aggregate.</param>
        /// <returns>true if the sequence was removed otherwise false.</returns>
        public bool Remove(Sequence sequence)
        {
            var found = false;
            Sequence[] oldSequences;
            Sequence[] newSequences;
            do
            {
                oldSequences = _sequencesRef.Value;
                int oldSize = oldSequences.Length;
                newSequences = new Sequence[oldSize - 1];

                int pos = 0;
                for (int i = 0; i < oldSize; i++)
                {
                    var testSequence = oldSequences[i];
                    if (sequence == testSequence && !found)
                    {
                        found = true;
                    }
                    else
                    {
                        newSequences[pos++] = testSequence;
                    }
                }

                if (!found)
                {
                    break;
                }
            }
            while (!_sequencesRef.CompareAndSet(oldSequences, newSequences));

            return found;
        }

        /// <summary>
        /// Get the size of the group.
        /// </summary>
        public int Size
        {
            get { return _sequencesRef.Value.Length; }
        }
    }
}