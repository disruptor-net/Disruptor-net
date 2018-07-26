using System;
using System.Runtime.CompilerServices;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class ValueRingBuffer<T> : RingBuffer, ISequenced, IValueDataProvider<T>
        where T : struct
    {
        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(ISequencer sequencer)
            : this(() => default(T), sequencer)
        {
        }

        /// <summary>
        /// Construct a ValueRingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the ring buffer</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the ring buffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public ValueRingBuffer(Func<T> eventFactory, ISequencer sequencer)
        : base(sequencer, typeof(T))
        {
            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            var entries = (T[]) _entries;
            for (int i = 0; i < _bufferSize; i++)
            {
                entries[_bufferPad + i] = eventFactory();
            }
        }

        /// <summary>
        /// Construct a ValueRingBuffer with a <see cref="MultiProducerSequencer"/> sequencer.
        /// </summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the ring buffer</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        public ValueRingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory, new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new multiple producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
        {
            return CreateMultiProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

            return new ValueRingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new single producer ValueRingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
        {
            return CreateSingleProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new ValueRingBuffer with the specified producer type.
        /// </summary>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static ValueRingBuffer<T> Create(ProducerType producerType, Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            switch (producerType)
            {
                case ProducerType.Single:
                    return CreateSingleProducer(factory, bufferSize, waitStrategy);
                case ProducerType.Multi:
                    return CreateMultiProducer(factory, bufferSize, waitStrategy);
                default:
                    throw new ArgumentOutOfRangeException(producerType.ToString());
            }
        }

        /// <summary>
        /// Get the event for a given sequence in the RingBuffer.
        /// 
        /// This call has 2 uses.  Firstly use this call when publishing to a ring buffer.
        /// After calling <see cref="RingBuffer.Next()"/> use this call to get hold of the
        /// preallocated event to fill with data before calling <see cref="RingBuffer.Publish(long)"/>.
        /// 
        /// Secondly use this call when consuming data from the ring buffer.  After calling
        /// <see cref="ISequenceBarrier.WaitFor"/> call this method with any value greater than
        /// that your current consumer sequence and less than or equal to the value returned from
        /// the <see cref="ISequenceBarrier.WaitFor"/> method.
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        /// <returns>the event for the given sequence</returns>
        public ref T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return ref Util.ReadValue<T>(_entries, _bufferPad + (int)(sequence & _indexMask));
            }
        }
        
        /// <summary>
        /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
        /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to claim.</param>
        /// <returns>the preallocated event.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ref T ClaimAndGetPreallocated(long sequence)
        {
            _sequencer.Claim(sequence);
            return ref this[sequence];
        }

        /// <summary>
        /// Publishes an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        public void PublishEvent<TTranslator>(TTranslator translator) where TTranslator : IValueEventTranslator<T>
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence);
        }

        /// <summary>
        /// Attempts to publish an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity
        /// was not available.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        public bool TryPublishEvent<TTranslator>(TTranslator translator) where TTranslator : IValueEventTranslator<T>
        {
            if (_sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        public void PublishEvents(IValueEventTranslator<T>[] translators)
        {
            PublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        public void PublishEvents(IValueEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            long finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        public bool TryPublishEvents(IValueEventTranslator<T>[] translators)
        {
            return TryPublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        public bool TryPublishEvents(IValueEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);

            if (_sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
                return true;
            }

            return false;
        }

        private void TranslateAndPublish<TTranslator>(TTranslator translator, long sequence) where TTranslator : IValueEventTranslator<T>
        {
            try
            {
                translator.TranslateTo(ref this[sequence], sequence);
            }
            finally
            {
                _sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublishBatch(IValueEventTranslator<T>[] translators, int batchStartsAt, int batchSize, long finalSequence)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    IValueEventTranslator<T> translator = translators[i];
                    translator.TranslateTo(ref this[sequence], sequence++);
                }
            }
            finally
            {
                _sequencer.Publish(initialSequence, finalSequence);
            }
        }
    }
}
