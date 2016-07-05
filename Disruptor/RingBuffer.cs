using System;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing an event being exchanged between event publisher and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class RingBuffer<T> where T : class 
    {
        private readonly int _indexMask;
        private readonly T[] _entries;
        private Sequencer _sequencer;

        /// <summary>
        /// Construct a RingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="bufferSize">the size of the ring buffer</param>
        /// <param name="sequencer">waiting strategy employed by processorsToTrack waiting on entries becoming available.</param>
        public RingBuffer(Func<T> eventFactory, int bufferSize, Sequencer sequencer)
        {
            _sequencer = sequencer;
            if (bufferSize.CeilingNextPowerOfTwo() != bufferSize)
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _indexMask = bufferSize - 1;
            _entries = new T[bufferSize];

            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i] = eventFactory();
            }
        }

        /// <summary>
        /// Construct a RingBuffer with default strategies of:
        /// <see cref="MultiThreadedLowContentionClaimStrategy"/> and <see cref="BlockingWaitStrategy"/></summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="bufferSize"></param>
        public RingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory, 
                   bufferSize, 
                   new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        ///<summary>
        /// Get the event for a given sequence in the RingBuffer.
        ///</summary>
        ///<param name="sequence">sequence for the event</param>
        public T this[long sequence] => _entries[(int)sequence & _indexMask];
    }
}