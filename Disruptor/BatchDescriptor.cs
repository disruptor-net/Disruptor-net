namespace Disruptor
{
    /// <summary>
    /// Used to record the batch of sequences claimed in a <see cref="Sequencer"/>.
    /// </summary>
    public class BatchDescriptor
    {
        ///<summary>
        /// Create a holder for tracking a batch of claimed sequences in a <see cref="Sequencer"/>
        ///</summary>
        ///<param name="size">size of the batch to claim</param>
        internal BatchDescriptor(int size)
        {
            Size = size;
        }

        /// <summary>
        /// Get the start sequence number of the batch.
        /// </summary>
        public long Start
        {
            get { return End - (Size - 1L); }
        }

        /// <summary>
        /// Get the size of the batch.
        /// </summary>
        public int Size { get; private set; }

        ///<summary>
        /// Get the end sequence number of the batch
        ///</summary>
        public long End { get; set; }
    }
}