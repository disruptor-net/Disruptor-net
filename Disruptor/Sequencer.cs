using System;

namespace Disruptor
{
    /// <summary>
    /// Coordinator for claiming sequences for access to a data structure while tracking dependent <see cref="Sequence"/>s
    /// </summary>
    public class Sequencer
    {
        /// <summary>
        /// Set to -1 as sequence starting point
        /// </summary>
        public const long InitialCursorValue = -1;

        private readonly Sequence _cursor = new Sequence(InitialCursorValue);
        private Sequence[] _gatingSequences;

        private readonly IClaimStrategy _claimStrategy;
        private readonly IWaitStrategy _waitStrategy;
        private readonly TimeoutException _timeoutExceptionInstance = new TimeoutException();

        /// <summary>
        /// Construct a Sequencer with the selected strategies.
        /// </summary>
        /// <param name="claimStrategy">claimStrategy for those claiming sequences.</param>
        /// <param name="waitStrategy">waitStrategy for those waiting on sequences.</param>
        public Sequencer(IClaimStrategy claimStrategy, IWaitStrategy waitStrategy)
        {
            _claimStrategy = claimStrategy;
            _waitStrategy = waitStrategy;
        }

        /// <summary>
        /// Set the sequences that will gate publishers to prevent the buffer wrapping.
        /// 
        /// This method must be called prior to claiming sequences otherwise
        /// a <see cref="NullReferenceException"/> will be thrown.
        /// </summary>
        /// <param name="sequences">sequences to be to be gated on.</param>
        public void SetGatingSequences(params Sequence[] sequences)
        {
            _gatingSequences = sequences;
        }

        /// <summary>
        /// Create a <see cref="ISequenceBarrier"/> that gates on the the cursor and a list of <see cref="Sequence"/>s
        /// </summary>
        /// <param name="sequencesToTrack"></param>
        /// <returns></returns>
        public ISequenceBarrier NewBarrier(params Sequence[] sequencesToTrack)
        {
            return new ProcessingSequenceBarrier(_waitStrategy, _cursor, sequencesToTrack);
        }

        /// <summary>
        /// Create a new {@link BatchDescriptor} that is the minimum of the requested size
        /// and the buffer size.
        /// </summary>
        /// <param name="size">size for the batch</param>
        /// <returns>the new <see cref="BatchDescriptor"/></returns>
        public BatchDescriptor NewBatchDescriptor(int size)
        {
            return new BatchDescriptor(Math.Min(size, _claimStrategy.BufferSize));
        }

        /// <summary>
        /// The capacity of the data structure to hold entries.
        /// </summary>
        public int BufferSize
        {
            get { return _claimStrategy.BufferSize; }
        }

        /// <summary>
        /// Get the value of the cursor indicating the published sequence.
        /// </summary>
        public long Cursor
        {
            get { return _cursor.Value; }
        }

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="availableCapacity">availableCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        public bool HasAvailableCapacity(int availableCapacity)
        {
            return _claimStrategy.HasAvailableCapacity(availableCapacity, _gatingSequences);
        }


        /// <summary>
        /// Claim the next event in sequence for publishing.
        /// </summary>
        /// <returns></returns>
        public long Next()
        {
            if (_gatingSequences == null)
            {
                throw new NullReferenceException("gatingSequences must be set before claiming sequences");
            }

            return _claimStrategy.IncrementAndGet(_gatingSequences);
        }

       
        /// <summary>
        /// Attempt to claim the next event in sequence for publishing.  Will return the
        /// number of the slot if there is at least the number of slots specified by the 
        /// availableCapacity paramter. 
        /// </summary>
        /// <param name="availableCapacity"></param>
        /// <returns>the claimed sequence value</returns>
        public long TryNext(int availableCapacity)
        {
            if (_gatingSequences == null)
            {
                throw new NullReferenceException("_gatingSequences must be set before claiming sequences");
            }

            if (availableCapacity < 1)
            {
                throw new ArgumentOutOfRangeException("availableCapacity", "Available capacity must be greater than 0");
            }
        
            return _claimStrategy.CheckAndIncrement(availableCapacity, 1, _gatingSequences);
        }

        /// <summary>
        /// Claim the next batch of sequence numbers for publishing.
        /// </summary>
        /// <param name="batchDescriptor">batchDescriptor to be updated for the batch range.</param>
        /// <returns>the updated batchDescriptor.</returns>
        public BatchDescriptor Next(BatchDescriptor batchDescriptor)
        {
            if (_gatingSequences == null)
            {
                throw new NullReferenceException("gatingSequences must be set before claiming sequences");
            }

            long sequence = _claimStrategy.IncrementAndGet(batchDescriptor.Size, _gatingSequences);
            batchDescriptor.End = sequence;
            return batchDescriptor;
        }

        /// <summary>
        /// Claim a specific sequence when only one publisher is involved.
        /// </summary>
        /// <param name="sequence">sequence to be claimed.</param>
        /// <returns>sequence just claimed.</returns>
        public long Claim(long sequence)
        {
            if (_gatingSequences == null)
            {
                throw new NullReferenceException("gatingSequences must be set before claiming sequences");
            }

            _claimStrategy.SetSequence(sequence, _gatingSequences);

            return sequence;
        }

        /// <summary>
        /// Publish an event and make it visible to <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="sequence">sequence to be published</param>
        public void Publish(long sequence)
        {
            Publish(sequence, 1);
        }

        /// <summary>
        /// Publish the batch of events in sequence.
        /// </summary>
        /// <param name="batchDescriptor">batchDescriptor to be published.</param>
        public void Publish(BatchDescriptor batchDescriptor)
        {
            Publish(batchDescriptor.End, batchDescriptor.Size);
        }

        /// <summary>
        /// Force the publication of a cursor sequence.
        /// 
        /// Only use this method when forcing a sequence and you are sure only one publisher exists.
        /// This will cause the cursor to advance to this sequence.
        /// </summary>
        /// <param name="sequence">sequence which is to be forced for publication.</param>
        public void ForcePublish(long sequence)
        {
            _cursor.LazySet(sequence);
            _waitStrategy.SignalAllWhenBlocking();
        }

        private void Publish(long sequence, int batchSize)
        {
            _claimStrategy.SerialisePublishing(sequence, _cursor, batchSize);
            _waitStrategy.SignalAllWhenBlocking();
        }

        /// <summary>
        /// Remaining capacity for the sequencer
        /// </summary>
        /// <returns></returns>
        public long RemainingCapacity()
        {
            long consumed = Util.GetMinimumSequence(_gatingSequences);
            long produced = _cursor.Value;
            return BufferSize - (produced - consumed);
        }
    }
}