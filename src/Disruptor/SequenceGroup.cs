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
    public class SequenceGroup : ISequence
    {
        /// <summary>Volatile in the Java version => always use Volatile.Read/Write or Interlocked methods to access this field.</summary>
        private ISequence[] _sequences = new ISequence[0];

        /// <summary>
        /// Get the minimum sequence value for the group.
        /// </summary>
        public long Value => Util.GetMinimumSequence(Volatile.Read(ref _sequences));

        /// <summary>
        /// Set all <see cref="Sequence"/>s in the group to a given value.
        /// </summary>
        /// <param name="value">value to set the group of sequences to.</param>
        public void SetValue(long value)
        {
            var sequences = Volatile.Read(ref _sequences);
            for (var i = 0; i < sequences.Length; i++)
            {
                sequences[i].SetValue(value);
            }
        }

        /// <summary>
        /// Performs a volatile write of this sequence.  The intent is a Store/Store barrier between this write and any previous
        /// write and a Store/Load barrier between this write and any subsequent volatile read. 
        /// </summary>
        /// <param name="value"></param>
        public void SetValueVolatile(long value)
        {
            var sequences = Volatile.Read(ref _sequences);
            for (var i = 0; i < sequences.Length; i++)
            {
                sequences[i].SetValueVolatile(value);
            }
        }

        public bool CompareAndSet(long expectedSequence, long nextSequence)
        {
            throw new NotImplementedException();
        }

        public long IncrementAndGet()
        {
            throw new NotImplementedException();
        }

        public long AddAndGet(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add a <see cref="Sequence"/> into this aggregate. This should only be used during
        /// initialisation. Use <see cref="SequenceGroup.AddWhileRunning"/>.
        /// </summary>
        /// <param name="sequence">sequence to be added to the aggregate.</param>
        public void Add(ISequence sequence)
        {
            ISequence[] oldSequences;
            ISequence[] newSequences;
            do
            {
                oldSequences = Volatile.Read(ref _sequences);
                var oldSize = oldSequences.Length;
                newSequences = new ISequence[oldSize + 1];
                Array.Copy(oldSequences, newSequences, oldSize);
                newSequences[oldSize] = sequence;
            }
            while (Interlocked.CompareExchange(ref _sequences, newSequences, oldSequences) != oldSequences);
        }

        /// <summary>
        /// Remove the first occurrence of the <see cref="Sequence"/> from this aggregate.
        /// </summary>
        /// <param name="sequence">sequence to be removed from this aggregate.</param>
        /// <returns>true if the sequence was removed otherwise false.</returns>
        public bool Remove(ISequence sequence)
        {
            return SequenceGroups.RemoveSequence(ref _sequences, sequence);
        }

        /// <summary>
        /// Get the size of the group.
        /// </summary>
        public int Size => Volatile.Read(ref _sequences).Length;
        
        /// <summary>
        /// Adds a sequence to the sequence group after threads have started to publish to
        /// the Disruptor.It will set the sequences to cursor value of the ringBuffer
        /// just after adding them.  This should prevent any nasty rewind/wrapping effects.
        /// </summary>
        /// <param name="cursored">The data structure that the owner of this sequence group will be pulling it's events from</param>
        /// <param name="sequence">The sequence to add</param>
        public void AddWhileRunning(ICursored cursored, Sequence sequence)
        {
            SequenceGroups.AddSequences(ref _sequences, cursored, sequence);
        }
    }
}