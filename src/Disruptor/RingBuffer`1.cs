﻿using System;
using System.Runtime.CompilerServices;
using Disruptor.Dsl;

#pragma warning disable 618

namespace Disruptor
{
    /// <summary>
    /// Ring based store of reusable entries containing the data representing
    /// an event being exchanged between event producer and <see cref="IEventProcessor"/>s.
    /// </summary>
    /// <typeparam name="T">implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class RingBuffer<T> : RingBuffer, IEventSequencer<T>, IEventSink<T>
        where T : class
    {
        /// <summary>
        /// Construct a RingBuffer with the full option set.
        /// </summary>
        /// <param name="eventFactory">eventFactory to create entries for filling the RingBuffer</param>
        /// <param name="sequencer">sequencer to handle the ordering of events moving through the RingBuffer.</param>
        /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
        public RingBuffer(Func<T> eventFactory, ISequencer sequencer)
        : base(sequencer, typeof(T), _bufferPadRef)
        {
            Fill(eventFactory);
        }

        private void Fill(Func<T> eventFactory)
        {
            var entries = (T[])_entries;
            for (int i = 0; i < _bufferSize; i++)
            {
                entries[_bufferPadRef + i] = eventFactory();
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
        public T this[long sequence]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                return Util.Read<T>(_entries, _bufferPadRef + (int)(sequence & _indexMask));
            }
        }

        /// <summary>
        /// Sets the cursor to a specific sequence and returns the preallocated entry that is stored there.  This
        /// can cause a data race and should only be done in controlled circumstances, e.g. during initialisation.
        /// </summary>
        /// <param name="sequence">the sequence to claim.</param>
        /// <returns>the preallocated event.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T ClaimAndGetPreallocated(long sequence)
        {
            _sequencerDispatcher.Sequencer.Claim(sequence);
            return this[sequence];
        }

        /// <summary>
        /// Determines if a particular entry is available.  Note that using this when not within a context that is
        /// maintaining a sequence barrier, it is likely that using this to determine if you can read a value is likely
        /// to result in a race condition and broken code.
        /// </summary>
        /// <param name="sequence">The sequence to identify the entry.</param>
        /// <returns><c>true</c> if the value can be read, <c>false</c> otherwise.</returns>
        [Obsolete("Please don't use this method. It probably won't do what you think that it does.")]
        public bool IsPublished(long sequence)
        {
            return _sequencerDispatcher.Sequencer.IsAvailable(sequence);
        }

        /// <summary>
        /// Creates an event poller for this ring buffer gated on the supplied sequences.
        /// </summary>
        /// <param name="gatingSequences">gatingSequences to be gated on.</param>
        /// <returns>A poller that will gate on this ring buffer and the supplied sequences.</returns>
        public EventPoller<T> NewPoller(params ISequence[] gatingSequences)
        {
            return _sequencerDispatcher.Sequencer.NewPoller(this, gatingSequences);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent(Disruptor.IEventTranslator{T})"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvent(IEventTranslator<T> translator)
        {
            long sequence = _sequencerDispatcher.Sequencer.Next();
            TranslateAndPublish(translator, sequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent(Disruptor.IEventTranslator{T})"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvent(IEventTranslator<T> translator)
        {
            if (_sequencerDispatcher.Sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A}(IEventTranslatorOneArg{T,A},A)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            long sequence = _sequencerDispatcher.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A}(IEventTranslatorOneArg{T,A},A)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0)
        {
            if (_sequencerDispatcher.Sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence, arg0);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A,B}(IEventTranslatorTwoArg{T,A,B},A,B)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            long sequence = _sequencerDispatcher.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A,B}(IEventTranslatorTwoArg{T,A,B},A,B)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1)
        {
            if (_sequencerDispatcher.Sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence, arg0, arg1);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A,B,C)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            long sequence = _sequencerDispatcher.Sequencer.Next();
            TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A,B,C)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2)
        {
            if (_sequencerDispatcher.Sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence, arg0, arg1, arg2);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvent(IEventTranslatorVararg{T},object[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            long sequence = _sequencerDispatcher.Sequencer.Next();
            TranslateAndPublish(translator, sequence, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvent(IEventTranslatorVararg{T},object[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvent(IEventTranslatorVararg<T> translator, params object[] args)
        {
            if (_sequencerDispatcher.Sequencer.TryNext(out var sequence))
            {
                TranslateAndPublish(translator, sequence, args);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslator{T}[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents(IEventTranslator<T>[] translators)
        {
            PublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslator{T}[],int,int)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);
            long finalSequence = _sequencerDispatcher.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslator{T}[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents(IEventTranslator<T>[] translators)
        {
            return TryPublishEvents(translators, 0, translators.Length);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslator{T}[],int,int)"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize)
        {
            CheckBounds(translators, batchStartsAt, batchSize);

            if (_sequencerDispatcher.Sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translators, batchStartsAt, batchSize, finalSequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A}(IEventTranslatorOneArg{T,A},A[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            PublishEvents(translator, 0, arg0.Length, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A}(IEventTranslatorOneArg{T,A},int,int,A[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);
            long finalSequence = _sequencerDispatcher.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A}(IEventTranslatorOneArg{T,A},A[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A}(IEventTranslatorOneArg{T,A},int,int,A[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0)
        {
            CheckBounds(arg0, batchStartsAt, batchSize);

            if (_sequencerDispatcher.Sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translator, arg0, batchStartsAt, batchSize, finalSequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},A[],B[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},int,int,A[],B[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);
            long finalSequence = _sequencerDispatcher.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},A[],B[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B}(IEventTranslatorTwoArg{T,A,B},int,int,A[],B[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1)
        {
            CheckBounds(arg0, arg1, batchStartsAt, batchSize);

            if (_sequencerDispatcher.Sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translator, arg0, arg1, batchStartsAt, batchSize, finalSequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A[],B[],C[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            PublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},int,int,A[],B[],C[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);
            long finalSequence = _sequencerDispatcher.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},A[],B[],C[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2)
        {
            return TryPublishEvents(translator, 0, arg0.Length, arg0, arg1, arg2);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents{A,B,C}(IEventTranslatorThreeArg{T,A,B,C},int,int,A[],B[],C[])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2)
        {
            CheckBounds(arg0, arg1, arg2, batchStartsAt, batchSize);

            if (_sequencerDispatcher.Sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translator, arg0, arg1, arg2, batchStartsAt, batchSize, finalSequence);
                return true;
            }

            return false;
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslatorVararg{T},object[][])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            PublishEventsInternal(translator, 0, args.Length, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.PublishEvents(IEventTranslatorVararg{T},int,int,object[][])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public void PublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            PublishEventsInternal(translator, batchStartsAt, batchSize, args);
        }

        private void PublishEventsInternal(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, object[][] args)
        {
            CheckBounds(batchStartsAt, batchSize, args);
            var finalSequence = _sequencerDispatcher.Sequencer.Next(batchSize);
            TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslatorVararg{T},object[][])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, params object[][] args)
        {
            return TryPublishEvents(translator, 0, args.Length, args);
        }

        /// <summary>
        /// <see cref="IEventSink{T}.TryPublishEvents(IEventTranslatorVararg{T},int,int,object[][])"/>
        /// </summary>
        [Obsolete(Constants.ObsoletePublicationApiMessage)]
        public bool TryPublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args)
        {
            CheckBounds(args, batchStartsAt, batchSize);

            if (_sequencerDispatcher.Sequencer.TryNext(batchSize, out var finalSequence))
            {
                TranslateAndPublishBatch(translator, batchStartsAt, batchSize, finalSequence, args);
                return true;
            }

            return false;
        }

        private void CheckBounds<TArray>(TArray[] translators, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(translators, batchStartsAt, batchSize);
        }

        private void CheckBounds<T1, T2>(T1[] arg1, T2[] arg2, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(arg1, batchStartsAt, batchSize);
            BatchOverRuns(arg2, batchStartsAt, batchSize);
        }

        private void CheckBounds<T1, T2, T3>(T1[] arg1, T2[] arg2, T3[] arg3, int batchStartsAt, int batchSize)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(arg1, batchStartsAt, batchSize);
            BatchOverRuns(arg2, batchStartsAt, batchSize);
            BatchOverRuns(arg3, batchStartsAt, batchSize);
        }

        private void CheckBounds(int batchStartsAt, int batchSize, object[][] args)
        {
            CheckBatchSizing(batchStartsAt, batchSize);
            BatchOverRuns(args, batchStartsAt, batchSize);
        }

        private void CheckBatchSizing(int batchStartsAt, int batchSize)
        {
            if (batchStartsAt < 0 || batchSize < 0)
            {
                throw new ArgumentException("Both batchStartsAt and batchSize must be positive but got: batchStartsAt " + batchStartsAt + " and batchSize " + batchSize);
            }
            if (batchSize > BufferSize)
            {
                throw new ArgumentException("The ring buffer cannot accommodate " + batchSize + " it only has space for " + BufferSize + " entities.");
            }
        }

        private static void BatchOverRuns<TArray>(TArray[] arg0, int batchStartsAt, int batchSize)
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
                _sequencerDispatcher.Sequencer.Publish(sequence);
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
                _sequencerDispatcher.Sequencer.Publish(sequence);
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
                _sequencerDispatcher.Sequencer.Publish(sequence);
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
                _sequencerDispatcher.Sequencer.Publish(sequence);
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
                _sequencerDispatcher.Sequencer.Publish(sequence);
            }
        }

        private void TranslateAndPublishBatch(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize, long finalSequence)
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
                _sequencerDispatcher.Sequencer.Publish(initialSequence, finalSequence);
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
                _sequencerDispatcher.Sequencer.Publish(initialSequence, finalSequence);
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
                _sequencerDispatcher.Sequencer.Publish(initialSequence, finalSequence);
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
                _sequencerDispatcher.Sequencer.Publish(initialSequence, finalSequence);
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
                _sequencerDispatcher.Sequencer.Publish(initialSequence, finalSequence);
            }
        }

        /// <summary>
        /// Increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
        /// This method will block until there is space available in the ring buffer.
        /// buffer
        /// <code>
        /// using (var scope = _ringBuffer.PublishEvent())
        /// {
        ///     var e = scope.Event();
        ///     // Do some work with the event.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnpublishedEventScope PublishEvent()
        {
            var sequence = Next();
            return new UnpublishedEventScope(this, sequence);
        }

        /// <summary>
        /// Try to increment the ring buffer sequence and return a scope that will publish the sequence on disposing.
        /// This method will not block if there is not space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.TryPublishEvent())
        /// {
        ///     if (!scope.TryGetEvent(out var eventRef))
        ///         return;
        ///
        ///     var e = eventRef.Event();
        ///     // Do some work with the event.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NullableUnpublishedEventScope TryPublishEvent()
        {
            var success = TryNext(out var sequence);
            return new NullableUnpublishedEventScope(success ? this : null, sequence);
        }

        /// <summary>
        /// Increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
        /// This method will block until there is space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.PublishEvents(2))
        /// {
        ///     var e1 = scope.Event(0);
        ///     var e2 = scope.Event(1);
        ///     // Do some work with the events.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public UnpublishedEventBatchScope PublishEvents(int count)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var endSequence = Next(count);
            return new UnpublishedEventBatchScope(this, endSequence + 1 - count, endSequence);
        }

        /// <summary>
        /// Try to increment the ring buffer sequence by <paramref name="count"/> and return a scope that will publish the sequences on disposing.
        /// This method will not block if there is not space available in the ring buffer.
        /// <code>
        /// using (var scope = _ringBuffer.TryPublishEvent(2))
        /// {
        ///     if (!scope.TryGetEvents(out var eventsRef))
        ///         return;
        ///
        ///     var e1 = eventRefs.Event(0);
        ///     var e2 = eventRefs.Event(1);
        ///     // Do some work with the events.
        /// }
        /// </code>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NullableUnpublishedEventBatchScope TryPublishEvents(int count)
        {
            if ((uint)count > _bufferSize)
                ThrowInvalidPublishCountException();

            var success = TryNext(count, out var endSequence);
            return new NullableUnpublishedEventBatchScope(success ? this : null, endSequence + 1 - count, endSequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// Publishes the sequence number on disposing.
        /// </summary>
        public readonly struct UnpublishedEventScope : IDisposable
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public UnpublishedEventScope(RingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            public long Sequence => _sequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Event() => _ringBuffer[_sequence];

            /// <summary>
            /// Publishes the sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => _ringBuffer.Publish(_sequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// Publishes the sequence numbers on disposing.
        /// </summary>
        public readonly struct UnpublishedEventBatchScope : IDisposable
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public UnpublishedEventBatchScope(RingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            public long StartSequence => _startSequence;
            public long EndSequence => _endSequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Event(int index) => _ringBuffer[_startSequence + index];

            /// <summary>
            /// Publishes the sequence number batch.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() => _ringBuffer.Publish(_startSequence, _endSequence);
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// Publishes the sequence number on disposing.
        /// </summary>
        public readonly struct NullableUnpublishedEventScope : IDisposable
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public NullableUnpublishedEventScope(RingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            /// <summary>
            /// Returns a value indicating whether the sequence was successfully claimed.
            /// </summary>
            public bool HasEvent => _ringBuffer != null;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            /// <returns>
            /// true if the sequence number was successfully claimed, false otherwise.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetEvent(out EventRef eventRef)
            {
                eventRef = new EventRef(_ringBuffer, _sequence);
                return _ringBuffer != null;
            }

            /// <summary>
            /// Publishes the sequence number if it was successfully claimed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (_ringBuffer != null)
                    _ringBuffer.Publish(_sequence);
            }
        }

        /// <summary>
        /// Holds an unpublished sequence number.
        /// </summary>
        public readonly struct EventRef
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _sequence;

            public EventRef(RingBuffer<T> ringBuffer, long sequence)
            {
                _ringBuffer = ringBuffer;
                _sequence = sequence;
            }

            public long Sequence => _sequence;

            /// <summary>
            /// Gets the event for the associated sequence number.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Event() => _ringBuffer[_sequence];
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// Publishes the sequence numbers on disposing.
        /// </summary>
        public readonly struct NullableUnpublishedEventBatchScope : IDisposable
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public NullableUnpublishedEventBatchScope(RingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            /// <summary>
            /// Returns a value indicating whether the sequence batch was successfully claimed.
            /// </summary>
            public bool HasEvents => _ringBuffer != null;

            /// <summary>
            /// Gets the events for the associated sequence number batch.
            /// </summary>
            /// <returns>
            /// true if the sequence batch was successfully claimed, false otherwise.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool TryGetEvents(out EventBatchRef eventRef)
            {
                eventRef = new EventBatchRef(_ringBuffer, _startSequence, _endSequence);
                return _ringBuffer != null;
            }

            /// <summary>
            /// Publishes the sequence batch if it was successfully claimed.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose()
            {
                if (_ringBuffer != null)
                    _ringBuffer.Publish(_startSequence, _endSequence);
            }
        }

        /// <summary>
        /// Holds an unpublished sequence number batch.
        /// </summary>
        public readonly struct EventBatchRef
        {
            private readonly RingBuffer<T> _ringBuffer;
            private readonly long _startSequence;
            private readonly long _endSequence;

            public EventBatchRef(RingBuffer<T> ringBuffer, long startSequence, long endSequence)
            {
                _ringBuffer = ringBuffer;
                _startSequence = startSequence;
                _endSequence = endSequence;
            }

            public long StartSequence => _startSequence;
            public long EndSequence => _endSequence;

            /// <summary>
            /// Gets the event for the associated sequence number and the specified index.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public T Event(int index) => _ringBuffer[_startSequence + index];
        }
    }
}
