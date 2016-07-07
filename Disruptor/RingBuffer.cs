using System;
using Disruptor.Dsl;

namespace Disruptor
{
    /// <summary>
    ///     Ring based store of reusable entries containing the data representing an event being exchanged between event
    ///     publisher and <see cref="IEventProcessor" />s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class RingBuffer<T> : IEventSequencer<T>, IEventSink<T>, ICursored
        where T : class
    {
        private readonly int _indexMask;
        private readonly T[] _entries;
        private readonly Sequencer _sequencer;

        /// <summary>
        ///     Construct a RingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="sequencer">waiting strategy employed by processorsToTrack waiting on entries becoming available.</param>
        public RingBuffer(Func<T> eventFactory, Sequencer sequencer)
        {
            _sequencer = sequencer;
            int bufferSize = sequencer.BufferSize;
            if (bufferSize.CeilingNextPowerOfTwo() != bufferSize)
            {
                throw new ArgumentException("bufferSize must be a power of 2");
            }

            _indexMask = bufferSize - 1;
            _entries = new T[bufferSize];

            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                _entries[i] = eventFactory();
            }
        }

        /// <summary>
        ///     Construct a RingBuffer with default strategies of:
        ///     <see cref="MultiThreadedLowContentionClaimStrategy" /> and <see cref="BlockingWaitStrategy" />
        /// </summary>
        /// <param name="eventFactory"> eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        public RingBuffer(Func<T> eventFactory, int bufferSize)
            : this(eventFactory,
                   new MultiProducerSequencer(bufferSize, new BlockingWaitStrategy()))
        {
        }

        /// <summary>
        ///     Create a new multiple producer RingBuffer using the default wait strategy  <see cref="BlockingWaitStrategy" />.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns></returns>
        public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            MultiProducerSequencer sequencer = new MultiProducerSequencer(bufferSize, waitStrategy);

            return new RingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        /// </summary>
        /// <param name="factory"></param>
        /// <param name="bufferSize"></param>
        /// <returns></returns>
        public static RingBuffer<T> CreateMultiProducer(Func<T> factory, int bufferSize)
        {
            return CreateMultiProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        ///     Create a new single producer RingBuffer with the specified wait strategy.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns></returns>
        public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize, IWaitStrategy waitStrategy)
        {
            SingleProducerSequencer sequencer = new SingleProducerSequencer(bufferSize, waitStrategy);

            return new RingBuffer<T>(factory, sequencer);
        }

        /// <summary>
        ///     Create a new single producer RingBuffer using the default wait strategy  {@link BlockingWaitStrategy}.
        /// </summary>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <returns></returns>
        public static RingBuffer<T> CreateSingleProducer(Func<T> factory, int bufferSize)
        {
            return CreateSingleProducer(factory, bufferSize, new BlockingWaitStrategy());
        }

        /// <summary>
        ///     Create a new Ring Buffer with the specified producer type (SINGLE or MULTI)
        /// </summary>
        /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
        /// <param name="factory">used to create the events within the ring buffer.</param>
        /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
        /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
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
        ///     Get the event for a given sequence in the RingBuffer.
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        // TODO: Any way to avoid the bounds check?
        public T this[long sequence] => _entries[(int)sequence & _indexMask];

        public int BufferSize { get { return _sequencer.BufferSize; } }

        public bool HasAvailableCapacity(int requiredCapacity)
        {
            return _sequencer.HasAvailableCapacity(requiredCapacity);
        }

        public long Next()
        {
            return _sequencer.Next();
        }

        public long Next(int n)
        {
            return _sequencer.Next(n);
        }

        public long TryNext()
        {
            return _sequencer.TryNext();
        }

        public long TryNext(int n)
        {
            return _sequencer.TryNext(n);
        }

        [Obsolete]
        public void ResetTo(long sequence)
        {
            _sequencer.Claim(sequence);
            _sequencer.Publish(sequence);
        }

        public T ClaimAndGetPreallocated(long sequence)
        {
            _sequencer.Claim(sequence);
            return this[sequence];
        }

        public bool IsPublished(long sequence)
        {
            return _sequencer.IsAvailable(sequence);
        }

        public void AddGatingSequences(params Sequence[] gatingSequences)
        {
            _sequencer.AddGatingSequences(gatingSequences);
        }

        public long GetMinimumGatingSequence()
        {
            return _sequencer.GetMinimumSequence();
        }

        /// <summary>
        /// Remove the specified sequence from this ringBuffer.
        /// </summary>
        /// <param name="sequence">sequence to be removed.</param>
        /// <returns><tt>true</tt> if this sequence was found, <tt>false</tt> otherwise.</returns>
        public bool RemoveGatingSequence(Sequence sequence)
        {
            return _sequencer.RemoveGatingSequence(sequence);
        }

        /// <summary>
        /// Create a new SequenceBarrier to be used by an EventProcessor to track which messages
        /// are available to be read from the ring buffer given a list of sequences to track.
        /// </summary>
        /// <param name="sequencesToTrack">the additional sequences to track</param>
        /// <returns>A sequence barrier that will track the specified sequences.</returns>
        public ISequenceBarrier NewBarrier(params Sequence[] sequencesToTrack)
        {
            return _sequencer.NewBarrier(sequencesToTrack);
        }

        /// <summary>
        /// Creates an event poller for this ring buffer gated on the supplied sequences.
        /// </summary>
        /// <param name="gatingSequences"></param>
        /// <returns>A poller that will gate on this ring buffer and the supplied sequences.</returns>
        public EventPoller<T> NewPoller(params Sequence[] gatingSequences)
        {
            return _sequencer.NewPoller(this, gatingSequences);
        }

        /// <summary>
        /// Get the current cursor value for the ring buffer.  The actual value recieved
        /// will depend on the type of {@link Sequencer} that is being used.
        /// </summary>
        public long Cursor => _sequencer.Cursor;

        public void PublishEvent(IEventTranslator<T> translator)
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence);
        }

        public bool TryPublishEvent(IEventTranslator<T> translator)
        {
            try
            {
                long sequence = _sequencer.TryNext();
                TranslateAndPublish(translator, sequence);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0);
        }

        public bool TryPublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            try
            {
                long sequence = _sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1);
        }

        public bool TryPublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            try
            {
                long sequence = _sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0, arg1);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
        }

        public bool TryPublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            try
            {
                long sequence = _sequencer.TryNext();
                TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            long sequence = _sequencer.Next();
            TranslateAndPublish(translator, sequence, args);
        }

        public bool TryPublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            try
            {
                long sequence = _sequencer.TryNext();
                TranslateAndPublish(translator, sequence, args);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvents(IEventTranslator<T>[] translators)
        {
            PublishEvents(translators, 0, translators.Length);
        }

        public void PublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            long finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
        }

        public bool TryPublishEvents(IEventTranslator<T>[] translators)
        {
            return TryPublishEvents(translators, 0, translators.Length);
        }

        public bool TryPublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            PublishEvents(translator, 0, arg0.Length, arg0);
        }

        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);
            long finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
        }

        public bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0);
        }

        public bool TryPublishEvents<A>(
            IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);
            long finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
        }

        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);
            long finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
        }

        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void PublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            PublishEventsInternal(translator, 0, args.Length, args);
        }

        public void PublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            PublishEventsInternal(translator, batchStartsAt, batchSize, args);
        }

        private void PublishEventsInternal(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, object[][] args)
        {
            CheckBounds(batchStartsAt, batchSize, args);
            var finalSequence = _sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
        }

        /// <summary>
        /// <see cref="EventSink.TryPublishEvents"/>
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            return TryPublishEvents(translator, 0, args.Length, args);
        }

        /// <summary>
        /// <see cref="EventSink.TryPublishEvents"/>
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            CheckBounds(args, batchStartsAt, batchSize);
            try
            {
                long finalSequence = _sequencer.TryNext(batchSize);
                TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
                return true;
            }
            catch (InsufficientCapacityException e)
            {
                return false;
            }
        }

        public void Publish(long sequence)
        {
            _sequencer.Publish(sequence);
        }

        /// <summary>
        /// Publish the specified sequences.  This action marks these particular
        /// messages as being available to be read.
        /// </summary>
        /// <param name="lo">the lowest sequence number to be published</param>
        /// <param name="hi">the highest sequence number to be published</param>
        public void Publish(long lo, long hi)
        {
            _sequencer.Publish(lo, hi);
        }

        /// <summary>
        /// Get the remaining capacity for this ringBuffer.
        /// </summary>
        /// <returns>The number of slots remaining.</returns>
        public long GetRemainingCapacity()
        {
            return _sequencer.GetRemainingCapacity();
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
                _sequencer.Publish(sequence);
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
                _sequencer.Publish(sequence);
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
                _sequencer.Publish(sequence);
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
                _sequencer.Publish(sequence);
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
                _sequencer.Publish(sequence);
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
                _sequencer.Publish(initialSequence, finalSequence);
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
                _sequencer.Publish(initialSequence, finalSequence);
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
                _sequencer.Publish(initialSequence, finalSequence);
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
                _sequencer.Publish(initialSequence, finalSequence);
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
                _sequencer.Publish(initialSequence, finalSequence);
            }
        }
    }
}