namespace Disruptor.Processing
{
    /// <summary>
    /// An IEventProcessor needs to poll for events from the <see cref="RingBuffer{T}"/>
    /// using the appropriate wait strategy. It is unlikely that you will need to implement this interface yourself.
    /// Look at using the <see cref="IEventHandler{T}"/> interface along with the pre-supplied BatchEventProcessor in the first
    /// instance.
    /// </summary>
    public interface IEventProcessor
    {
        /// <summary>
        /// Return a reference to the <see cref="ISequence"/> being used by this <see cref="IEventProcessor"/>
        /// </summary>
        ISequence Sequence { get; }

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.Alert"/> to notify the thread to check status.
        /// </summary>
        void Halt();

        /// <summary>
        /// Starts this instance 
        /// </summary>
        void Run();

        /// <summary>
        /// Gets if the processor is running
        /// </summary>
        bool IsRunning { get; }
    }
}