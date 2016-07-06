using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// A <see cref="Sequence"/> group that can dynamically have <see cref="Sequence"/>s added and removed while being
    /// thread safe.
    /// 
    /// The <see cref="SequenceGroup.Value"/> get and set methods are lock free and can be
    /// concurrently called with the <see cref="SequenceGroup.Add"/> and <see cref="SequenceGroup.Remove"/>.
    /// </summary>
    public class SequenceGroup : Sequence
    {
        private Volatile.Reference<Sequence[]> _sequencesRef = new Volatile.Reference<Sequence[]>(new Sequence[0]);
        
        /// <summary>
        /// Default constructor
        /// </summary>
        public SequenceGroup() : base(InitialCursorValue)
        {
        }

        /// <summary>
        /// Get the minimum sequence value for the group.
        /// Set all <see cref="Sequence"/>s in the group to a given value.
        /// </summary>
        public override long Value
        {
            get { return Util.GetMinimumSequence(_sequencesRef.ReadFullFence()); }
            set
            {
                var sequences = _sequencesRef.ReadFullFence();
                for (var i = 0; i < sequences.Length; i++)
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
            var sequences = _sequencesRef.ReadFullFence();
            for (int i = 0; i < sequences.Length; i++)
            {
                sequences[i].LazySet(value);
            }
        }

        /// <summary>
        /// Add a <see cref="Sequence"/> into this aggregate. This should only be used during
        /// initialisation. Use <see cref="Sequence.AddWhileRunning"/>.
        /// </summary>
        /// <param name="sequence">sequence to be added to the aggregate.</param>
        public void Add(Sequence sequence)
        {
            Sequence[] oldSequences;
            Sequence[] newSequences;
            do
            {
                oldSequences = _sequencesRef.ReadFullFence();
                var oldSize = oldSequences.Length;
                newSequences = new Sequence[oldSize + 1];
                Array.Copy(oldSequences, newSequences, oldSize);
                newSequences[oldSize] = sequence;
            }
            while (!_sequencesRef.AtomicCompareExchange(newSequences, oldSequences));
        }

        /// <summary>
        /// Remove the first occurrence of the <see cref="Sequence"/> from this aggregate.
        /// </summary>
        /// <param name="sequence">sequence to be removed from this aggregate.</param>
        /// <returns>true if the sequence was removed otherwise false.</returns>
        public bool Remove(Sequence sequence) => SequenceGroups.RemoveSequence(_sequencesRef, sequence);

        /// <summary>
        /// Get the size of the group.
        /// </summary>
        public int Size => _sequencesRef.ReadFullFence().Length;
        
        /// <summary>
        /// Adds a sequence to the sequence group after threads have started to publish to
        /// the Disruptor.It will set the sequences to cursor value of the ringBuffer
        /// just after adding them.  This should prevent any nasty rewind/wrapping effects.
        /// </summary>
        /// <param name="cursored">The data structure that the owner of this sequence group will be pulling it's events from</param>
        /// <param name="sequence">The sequence to add</param>
        public void AddWhileRunning(ICursored cursored, Sequence sequence)
        {
            SequenceGroups.AddSequences(_sequencesRef, cursored, sequence);
        }
    }
}