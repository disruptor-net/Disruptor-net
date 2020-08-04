namespace Disruptor
{
    public interface ISequenced
    {
        /// <summary>
        /// Gets the capacity of the data structure to hold entries.
        /// </summary>
        int BufferSize { get; }

        /// <summary>
        /// Has the buffer got capacity to allocate another sequence.  This is a concurrent
        /// method so the response should only be taken as an indication of available capacity.
        /// </summary>
        /// <param name="requiredCapacity">requiredCapacity in the buffer</param>
        /// <returns>true if the buffer has the capacity to allocate the next sequence otherwise false.</returns>
        bool HasAvailableCapacity(int requiredCapacity);

        /// <summary>
        /// Get the remaining capacity for this sequencer. return The number of slots remaining.
        /// </summary>
        long GetRemainingCapacity();

        /// <summary>
        /// Claim an available sequence in the ring buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Calls of this method should ensure that they always publish the sequence afterward.
        /// </para>
        /// <para>
        /// If there is not enough space available in the ring buffer, this method will block and spin-wait, which can generate high CPU usage.
        /// Consider using <see cref="TryNext(out long)"/> with your own waiting policy if you need to change this behavior.
        /// </para>
        /// </remarks>
        /// <returns>The claimed sequence number.</returns>
        long Next();

        /// <summary>
        /// Claim a range of <paramref name="n"/> available sequences in the ring buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Calls of this method should ensure that they always publish the sequences afterward.
        /// </para>
        /// <para>
        /// If there is not enough space available in the ring buffer, this method will block and spin-wait, which can generate high CPU usage.
        /// Consider using <see cref="TryNext(int, out long)"/> with your own waiting policy if you need to change this behavior.
        /// </para>
        /// </remarks>
        /// <param name="n">number of slots to claim</param>
        /// <returns>The sequence number of the highest slot claimed.</returns>
        long Next(int n);

        /// <summary>
        /// Try to claim an available sequence in the ring buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Calls of this method should ensure that they always publish the sequence afterward.
        /// </para>
        /// <para>
        /// If there is not enough space available in the ring buffer, this method will return false.
        /// </para>
        /// </remarks>
        /// <param name="sequence">the next sequence to publish to</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        bool TryNext(out long sequence);

        /// <summary>
        /// Try to claim a range of <paramref name="n"/> available sequences in the ring buffer.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Calls of this method should ensure that they always publish the sequences afterward.
        /// </para>
        /// <para>
        /// If there is not enough space available in the ring buffer, this method will return false.
        /// </para>
        /// </remarks>
        /// <param name="n">number of slots to claim</param>
        /// <param name="sequence">sequence number of the highest slot claimed</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        bool TryNext(int n, out long sequence);

        /// <summary>
        /// Publishes a sequence. Call when the event has been filled.
        /// </summary>
        /// <param name="sequence">the sequence to be published.</param>
        void Publish(long sequence);

        /// <summary>
        /// Batch publish sequences.  Called when all of the events have been filled.
        /// </summary>
        /// <param name="lo">first sequence number to publish</param>
        /// <param name="hi">last sequence number to publish</param>
        void Publish(long lo, long hi);
    }
}
