using System;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class RingBuffer<T> : IEventSequencer<T>, IEventSink<T>, ICursored
        where T : class
    {
        private static readonly unsafe int _bufferPad = 128 / sizeof(IntPtr);

        private RingBufferFields _fields;

        /// <summary>
        /// Construct a RingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the RingBuffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public RingBuffer(Func<T> eventFactory, ISequencer sequencer)
        {
            _fields.Sequencer = sequencer;
            _fields.BufferSize = sequencer.BufferSize;

            if (_fields.BufferSize < 1)
            {
                throw new ArgumentException("bufferSize must not be less than 1");
            }
            if (_fields.BufferSize.CeilingNextPowerOfTwo() != _fields.BufferSize)
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _fields.IndexMask = _fields.BufferSize - 1;
            _fields.Entries = new object[_fields.BufferSize + 2 * _bufferPad];

            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            for (int i = 0; i < _fields.BufferSize; i++)
            {
                _fields.Entries[_bufferPad + i] = eventFactory();
            }
        }

        /// <summary>
        /// Construct a RingBuffer with a <see cref="MultiProducerSequencer"/> sequencer.
        /// </summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        public RingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory, new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        /// <summary>
        /// Create a new multiple producer RingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

            return new RingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new multiple producer RingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
        {
            return CreateMultiProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new single producer RingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

            return new RingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// Create a new single producer RingBuffer using the default wait strategy <see cref="BlockingWaitStrategy"/>.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
        {
            return CreateSingleProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        /// Create a new Ring Buffer with the specified producer type (SINGLE or MULTI)
        /// </summary>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns>a constructed ring buffer.</returns>
        /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public static RingBuffer<T> Create(ProducerType producerType, Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
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
        /// After calling <see cref="Next()"/> use this call to get hold of the
        /// preallocated event to fill with data before calling <see cref="Publish(long)"/>.
        /// 
        /// Secondly use this call when consuming data from the ring buffer.  After calling
        /// <see cref="ISequenceBarrier.WaitFor"/> call this method with any value greater than
        /// that your current consumer sequence and less than or equal to the value returned from
        /// the <see cref="ISequenceBarrier.WaitFor"/> method.
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        /// <returns>the event for the given sequence</returns>
        // TODO: Any way to avoid the bounds check?
        public T this[long sequence] => (T)_fields.Entries[_bufferPad + ((int)sequence & _fields.IndexMask)];

        /// <summary>
        /// Gets the size of the buffer.
        /// </summary>
        public int BufferSize => _fields.BufferSize;

        /// <summary>
        /// Given specified <paramref name="requiredCapacity"/> determines if that amount of space
        /// is available.  Note, you can not assume that if this method returns <c>true</c>
        /// that a call to <see cref="Next()"/> will not block.  Especially true if this
        /// ring buffer is set up to handle multiple producers.
        /// </summary>
        /// <param name="requiredCapacity">The capacity to check for.</param>
        /// <returns><c>true</c> if the specified <paramref name="requiredCapacity"/> is available <c>false</c> if not.</returns>
        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return _fields.Sequencer.HasAvailableCapacity(requiredCapacity);
        }

        /// <summary>
        /// Increment and return the next sequence for the ring buffer.  Calls of this
        /// method should ensure that they always publish the sequence afterward. E.g.
        /// <pre>
        /// long sequence = ringBuffer.next();
        /// try
        /// {
        ///     Event e = ringBuffer.get(sequence);
        ///     // Do some work with the event.
        /// }
        /// finally
        /// {
        ///     ringBuffer.publish(sequence);
        /// }
        /// </pre>
        /// </summary>
        /// <returns>The next sequence to publish to.</returns>
        public long Next()
        {
            return _fields.Sequencer.Next();
        }

        /// <summary>
        /// The same functionality as <see cref="Next()"/>, but allows the caller to claim
        /// the next n sequences.
        /// </summary>
        /// <param name="n">number of slots to claim</param>
        /// <returns>sequence number of the highest slot claimed</returns>
        public long Next(int n)
        {
            return _fields.Sequencer.Next(n);
        }

        /// <summary>
        /// Increment and return the next sequence for the ring buffer.  Calls of this
        /// method should ensure that they always publish the sequence afterward. E.g.
        /// <pre>
        /// long sequence = ringBuffer.next();
        /// try
        /// {
        ///     Event e = ringBuffer.get(sequence);
        ///     // Do some work with the event.
        /// }
        /// finally
        /// {
        ///     ringBuffer.publish(sequence);
        /// }
        /// </pre>
        /// This method will not block if there is not space available in the ring
        /// buffer, instead it will throw an <see cref="InsufficientCapacityException"/>.
        /// </summary>
        /// <returns>The next sequence to publish to.</returns>
        /// <exception cref="InsufficientCapacityException">if the necessary space in the ring buffer is not available</exception>
        public long TryNext()
        {
            return _fields.Sequencer.TryNext();
        }

        /// <summary>
        /// The same functionality as <see cref="TryNext(int)"/>, but allows the caller to attempt
        /// to claim the next n sequences.
        /// </summary>
        /// <param name="n">number of slots to claim</param>
        /// <returns>sequence number of the highest slot claimed</returns>
        /// <exception cref="InsufficientCapacityException">if the necessary space in the ring buffer is not available</exception>
        public long TryNext(int n)
        {
            return _fields.Sequencer.TryNext(n);
        }

        /// <summary>
        /// Increment and return the next sequence for the ring buffer.  Calls of this
        /// method should ensure that they always publish the sequence afterward. E.g.
        /// <pre>
        /// long sequence = ringBuffer.next();
        /// try
        /// {
        ///     Event e = ringBuffer.get(sequence);
        ///     // Do some work with the event.
        /// }
        /// finally
        /// {
        ///     ringBuffer.publish(sequence);
        /// }
        /// </pre>
        /// This method will not block if there is not space available in the ring
        /// buffer, instead it will return false.
        /// </summary>
        /// <param name="sequence">the next sequence to publish to</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        public bool TryNext(out long sequence)
        {
            return _fields.Sequencer.TryNext(out sequence);
        }

        /// <summary>
        /// The same functionality as <see cref="TryNext(out long)"/>, but allows the caller to attempt
        /// to claim the next n sequences.
        /// </summary>
        /// <param name="n">number of slots to claim</param>
        /// <param name="sequence">sequence number of the highest slot claimed</param>
        /// <returns>true if the necessary space in the ring buffer is not available, otherwise false.</returns>
        public bool TryNext(int n, out long sequence)
        {
            return _fields.Sequencer.TryNext(n, out sequence);
        }

        /// <summary>
        /// Resets the cursor to a specific value.  This can be applied at any time, but it is worth noting
        /// that it can cause a data race and should only be used in controlled circumstances.  E.g. during
        /// initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to reset too.</param>
        [Obsolete]
        public void ResetTo(long sequence)
        {
            _fields.Sequencer.Claim(sequence);
            _fields.Sequencer.Publish(sequence);
        }

        /// <summary>
        /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
        /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to claim.</param>
        /// <returns>the preallocated event.</returns>
        public T ClaimAndGetPreallocated(long sequence)
        {
            _fields.Sequencer.Claim(sequence);
            return this[sequence];
        }

        /// <summary>
        /// Determines if a particular entry is available.  Note that using this when not within a context that is
        /// maintaining a sequence barrier, it is likely that using this to determine if you can read a value is likely
        /// to result in a race condition and broken code.
        /// </summary>
        /// <param name="sequence">The sequence to identify the entry.</param>
        /// <returns><c>true</c> if the value can be read, <c>false</c> otherwise.</returns>
        [Obsolete("Please don't use this method.  It probably won't do what you think that it does.")]
        public bool IsPublished(long sequence)
        {
            return _fields.Sequencer.IsAvailable(sequence);
        }

        /// <summary>
        /// Add the specified gating sequences to this instance of the Disruptor.  They will
        /// safely and atomically added to the list of gating sequences.
        /// </summary>
        /// <param name="gatingSequences">the sequences to add.</param>
        public void AddGatingSequences(params ISequence[] gatingSequences)
        {
            _fields.Sequencer.AddGatingSequences(gatingSequences);
        }

        /// <summary>
        /// Get the minimum sequence value from all of the gating sequences
        /// added to this ringBuffer.
        /// </summary>
        /// <returns>the minimum gating sequence or the cursor sequence if no sequences have been added.</returns>
        public long GetMinimumGatingSequence()
        {
            return _fields.Sequencer.GetMinimumSequence();
        }

        /// <summary>
        /// Remove the specified sequence from this ringBuffer.
        /// </summary>
        /// <param name="sequence">sequence to be removed.</param>
        /// <returns><c>true</c> if this sequence was found, <c>false</c> otherwise.</returns>
        public bool RemoveGatingSequence(ISequence sequence)
        {
            return _fields.Sequencer.RemoveGatingSequence(sequence);
        }

        /// <summary>
        /// Create a new SequenceBarrier to be used by an EventProcessor to track which messages
        /// are available to be read from the ring buffer given a list of sequences to track.
        /// </summary>
        /// <param name="sequencesToTrack">the additional sequences to track</param>
        /// <returns>A sequence barrier that will track the specified sequences.</returns>
        public ISequenceBarrier NewBarrier(params ISequence[] sequencesToTrack)
        {
            return _fields.Sequencer.NewBarrier(sequencesToTrack);
        }

        /// <summary>
        /// Creates an event poller for this ring buffer gated on the supplied sequences.
        /// </summary>
        /// <param name="gatingSequences">gatingSequences to be gated on.</param>
        /// <returns>A poller that will gate on this ring buffer and the supplied sequences.</returns>
        public EventPoller<T> NewPoller(params ISequence[] gatingSequences)
        {
            return _fields.Sequencer.NewPoller(this, gatingSequences);
        }

        /// <summary>
        /// Get the current cursor value for the ring buffer.  The actual value received
        /// will depend on the type of <see cref="ISequencer"/> that is being used.
        /// </summary>
        public long Cursor => _fields.Sequencer.Cursor;

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent(Disruptor.IEventTranslator{T})"/>
        /// </summary>
        public void PublishEvent(IEventTranslator<T> translator)
        {
            long sequence = _fields.Sequencer.Next();
            TranslateAndPublish(translator, sequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent(Disruptor.IEventTranslator{T})"/>
        /// </summary>
        public bool TryPublishEvent(IEventTranslator<T> translator)
        {
            try
            {
                long sequence = _fields.Sequencer.TryNext();
                TranslateAndPublish(translator, sequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A}(IEventTranslatorOneArg{T,A},A)"/>
        /// </summary>
        public void PublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            long sequence = _fields.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A}(IEventTranslatorOneArg{T,A},A)"/>
        /// </summary>
        public bool TryPublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            try
            {
                long sequence = _fields.Sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A,B}(IEventTranslatorTwoArg{T,A,B},A,B)"/>
        /// </summary>
        public void PublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            long sequence = _fields.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A,B}(IEventTranslatorTwoArg{T,A,B},A,B)"/>
        /// </summary>
        public bool TryPublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            try
            {
                long sequence = _fields.Sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0, arg1);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A,B,C)"/>
        /// </summary>
        public void PublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            long sequence = _fields.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A,B,C)"/>
        /// </summary>
        public bool TryPublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            try
            {
                long sequence = _fields.Sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent(IEventTranslatorVararg{T},object[])"/>
        /// </summary>
        public void PublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            long sequence = _fields.Sequencer.Next();
            TranslateAndPublish(translator, sequence, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent(IEventTranslatorVararg{T},object[])"/>
        /// </summary>
        public bool TryPublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            try
            {
                long sequence = _fields.Sequencer.TryNext();
                TranslateAndPublish(translator, sequence, args);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslator{T}[])"/>
        /// </summary>
        public void PublishEvents(IEventTranslator<T>[] translators)
        {
            PublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslator{T}[],int,int)"/>
        /// </summary>
        public void PublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            long finalSequence = _fields.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslator{T}[])"/>
        /// </summary>
        public bool TryPublishEvents(IEventTranslator<T>[] translators)
        {
            return TryPublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslator{T}[],int,int)"/>
        /// </summary>
        public bool TryPublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _fields.Sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A}(IEventTranslatorOneArg{T,A},A[])"/>
        /// </summary>
        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            PublishEvents(translator, 0, arg0.Length, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A}(IEventTranslatorOneArg{T,A},int,int,A[])"/>
        /// </summary>
        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);
            long finalSequence = _fields.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A}(IEventTranslatorOneArg{T,A},A[])"/>
        /// </summary>
        public bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A}(IEventTranslatorOneArg{T,A},int,int,A[])"/>
        /// </summary>
        public bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _fields.Sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},A[],B[])"/>
        /// </summary>
        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},int,int,A[],B[])"/>
        /// </summary>
        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);
            long finalSequence = _fields.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},A[],B[])"/>
        /// </summary>
        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},int,int,A[],B[])"/>
        /// </summary>
        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _fields.Sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A[],B[],C[])"/>
        /// </summary>
        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},int,int,A[],B[],C[])"/>
        /// </summary>
        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);
            long finalSequence = _fields.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A[],B[],C[])"/>
        /// </summary>
        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},int,int,A[],B[],C[])"/>
        /// </summary>
        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _fields.Sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslatorVararg{T},object[][])"/>
        /// </summary>
        public void PublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            PublishEventsInternal(translator, 0, args.Length, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslatorVararg{T},int,int,object[][])"/>
        /// </summary>
        public void PublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            PublishEventsInternal(translator, batchStartsAt, batchSize, args);
        }

        private void PublishEventsInternal(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, object[][] args)
        {
            CheckBounds(batchStartsAt, batchSize, args);
            var finalSequence = _fields.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslatorVararg{T},object[][])"/>
        /// </summary>
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            return TryPublishEvents(translator, 0, args.Length, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslatorVararg{T},int,int,object[][])"/>
        /// </summary>
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            CheckBounds(args, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _fields.Sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
                return true;
            }
            catch (InsufficientCapacityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Publish the specified sequence.  This action marks this particular
        /// message as being available to be read.
        /// </summary>
        /// <param name="sequence">the sequence to publish.</param>
        public void Publish(long sequence)
        {
            _fields.Sequencer.Publish(sequence);
        }

        /// <summary>
        /// Publish the specified sequences.  This action marks these particular
        /// messages as being available to be read.
        /// </summary>
        /// <param name="lo">the lowest sequence number to be published</param>
        /// <param name="hi">the highest sequence number to be published</param>
        public void Publish(long lo, long hi)
        {
            _fields.Sequencer.Publish(lo, hi);
        }

        /// <summary>
        /// Get the remaining capacity for this ringBuffer.
        /// </summary>
        /// <returns>The number of slots remaining.</returns>
        public long GetRemainingCapacity()
        {
            return _fields.Sequencer.GetRemainingCapacity();
        }

        private void CheckBounds(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(translators, batchStartsAt, batchSize);
        }

        private void CheckBatchSizing(int batchStartsAt, int batchSize)
        {
            if (batchStartsAt < 0 || batchSize < 0)
            {
                throw new ArgumentException("Both batchStartsAt and batchSize must be positive but got: batchStartsAt " + batchStartsAt + " and batchSize " + batchSize);
            }
            else if (batchSize > BufferSize)
            {
                throw new ArgumentException("The ring buffer cannot accommodate " + batchSize + " it only has space for " + BufferSize + " entities.");
            }
        }

        private void CheckBounds<A>(A[] arg0, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(arg0, batchStartsAt, batchSize);
        }

        private void CheckBounds<A, B>(A[] arg0, B[] arg1, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(arg0, batchStartsAt, batchSize);
            BatchOverRuns(arg1, batchStartsAt, batchSize);
        }

        private void CheckBounds<A, B, C>(
            A[] arg0, B[] arg1, C[] arg2, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(arg0, batchStartsAt, batchSize);
            BatchOverRuns(arg1, batchStartsAt, batchSize);
            BatchOverRuns(arg2, batchStartsAt, batchSize);
        }

        private void CheckBounds(int batchStartsAt, int batchSize, object[][] args)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(args, batchStartsAt, batchSize);
        }

        private static void BatchOverRuns<A>(A[] arg0, int batchStartsAt, int batchSize)
        {
            if (batchStartsAt + batchSize > arg0.Length)
            {
                throw new ArgumentException(
                    "A batchSize of: " + batchSize +
                    " with batchStatsAt of: " + batchStartsAt +
                    " will overrun the available number of arguments: " + (arg0.Length - batchStartsAt));
            }
        }

        private void TranslateAndPublish(IEventTranslator<T> translator, long sequence)
        {
            try
            {
                translator.TranslateTo(this[sequence], sequence);
            }
            finally
            {
                _fields.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublish<A>(IEventTranslatorOneArg<T, A> translator, long sequence, A arg0)
        {
            try
            {
                translator.TranslateTo(this[sequence], sequence, arg0);
            }
            finally
            {
                _fields.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublish<A, B>(IEventTranslatorTwoArg<T, A, B> translator, long sequence, A arg0, B arg1)
        {
            try
            {
                translator.TranslateTo(this[sequence], sequence, arg0, arg1);
            }
            finally
            {
                _fields.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublish<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, long sequence,
                                                  A arg0, B arg1, C arg2)
        {
            try
            {
                translator.TranslateTo(this[sequence], sequence, arg0, arg1, arg2);
            }
            finally
            {
                _fields.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublish(IEventTranslatorVararg<T> translator, long sequence, params object[] args)
        {
            try
            {
                translator.TranslateTo(this[sequence], sequence, args);
            }
            finally
            {
                _fields.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublishBatch(IEventTranslator<T>[] translators, int batchStartsAt,
                                              int batchSize, long finalSequence)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    IEventTranslator<T> translator = translators[i];
                    translator.TranslateTo(this[sequence], sequence++);
                }
            }
            finally
            {
                _fields.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        private void TranslateAndPublishBatch<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0,
                                                 int batchStartsAt, int batchSize, long finalSequence)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    translator.TranslateTo(this[sequence], sequence++, arg0[i]);
                }
            }
            finally
            {
                _fields.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        private void TranslateAndPublishBatch<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0,
                                                    B[] arg1, int batchStartsAt, int batchSize,
                                                    long finalSequence)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    translator.TranslateTo(this[sequence], sequence++, arg0[i], arg1[i]);
                }
            }
            finally
            {
                _fields.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        private void TranslateAndPublishBatch<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator,
                                                       A[] arg0, B[] arg1, C[] arg2, int batchStartsAt,
                                                       int batchSize, long finalSequence)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    translator.TranslateTo(this[sequence], sequence++, arg0[i], arg1[i], arg2[i]);
                }
            }
            finally
            {
                _fields.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        private void TranslateAndPublishBatch(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, long finalSequence, object[][] args)
        {
            long initialSequence = finalSequence - (batchSize - 1);
            try
            {
                long sequence = initialSequence;
                int batchEndsAt = batchStartsAt + batchSize;
                for (int i = batchStartsAt; i < batchEndsAt; i++)
                {
                    translator.TranslateTo(this[sequence], sequence++, args[i]);
                }
            }
            finally
            {
                _fields.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        public override string ToString()
        {
            return "RingBuffer{" +
                   "bufferSize=" + _fields.BufferSize +
                   ", sequencer=" + _fields.Sequencer +
                   "}";
        }
    }
}