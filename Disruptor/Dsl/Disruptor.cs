using System;
using System.Threading;

namespace Disruptor.Dsl
{
    /// <summary>
    /// A DSL-style API for setting up the disruptor pattern around a ring buffer.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public class Disruptor<T> where T : class
    {
        private readonly RingBuffer<T> _ringBuffer;
        private readonly IExecutor _executor;
        private readonly ConsumerRepository<T> _consumerRepository = new ConsumerRepository<T>();
        private volatile int _running;
        private readonly EventPublisher<T> _eventPublisher;
        private IExceptionHandler _exceptionHandler;

        /// <summary>
        /// Create a new Disruptor.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="ringBufferSize">the size of the ring buffer, must be a power of 2.</param>
        /// <param name="executor">an <see cref="IExecutor"/> used to execute processors.</param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, IExecutor executor)
            : this(new RingBuffer<T>(eventFactory, ringBufferSize), executor)
        {
        }

        /// <summary>
        /// Create a new Disruptor.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer.</param>
        /// <param name="claimStrategy">the claim strategy to use for the ring buffer.</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer.</param>
        /// <param name="executor">an <see cref="IExecutor"/> used to execute processors.</param>
        public Disruptor(Func<T> eventFactory, 
                         IClaimStrategy claimStrategy,
                         IWaitStrategy waitStrategy,
                         IExecutor executor)
            : this(new RingBuffer<T>(eventFactory, claimStrategy, waitStrategy), executor)
        {
        }

        private Disruptor(RingBuffer<T> ringBuffer, IExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));

            _ringBuffer = ringBuffer;
            _executor = executor;
            _eventPublisher = new EventPublisher<T>(ringBuffer);
        }

        /// <summary>
        /// Set up custom <see cref="IEventProcessor"/>s to handle events from the ring buffer. The Disruptor will
        /// automatically start these processors when <see cref="Disruptor{T}.Start"/> is called.
        /// </summary>
        /// <param name="handlers">handlers the event handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
        {
            return CreateEventProcessors(new Sequence[0], handlers);
        }

        /// <summary>
        /// Set up custom <see cref="IEventProcessor"/> to handle events from the ring buffer. The Disruptor will
        /// automatically start those processors when <see cref="Disruptor{T}.Start"/> is called.
        /// </summary>
        /// <param name="processors">the event processors that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventProcessor[] processors)
        {
            foreach (var eventProcessor in processors)
            {
                _consumerRepository.Add(eventProcessor);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, processors);
        }

        /// <summary>
        /// Specify an <see cref="IExceptionHandler"/> to be used for any future event handlers.
        /// Note that only <see cref="IEventHandler{T}"/>s set up after calling this method will use the <see cref="IExceptionHandler"/>.
        /// </summary>
        /// <param name="exceptionHandler"></param>
        public void HandleExceptionsWith(IExceptionHandler exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Override the default <see cref="IExceptionHandler"/> for a specific <see cref="IEventHandler{T}"/>.
        /// </summary>
        /// <param name="eventHandler"> the <see cref="IEventHandler{T}"/> to set a different <see cref="IExceptionHandler"/> for.</param>
        /// <returns>an <see cref="ExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call.</returns>
        public ExceptionHandlerSetting<T> HandleExceptionsFor(IEventHandler<T> eventHandler)
        {
            return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
        }

        /// <summary>
        /// Create a group of <see cref="IEventHandler{T}"/>s to be used as a dependency.
        /// </summary>
        /// <param name="handlers">the <see cref="IEventHandler{T}"/>s, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
        ///                        that will form the <see cref="ISequenceBarrier"/> for subsequent handlers or processors.
        /// </param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a <see cref="ISequenceBarrier"/> over the specified <see cref="IEventHandler{T}"/>s.</returns>
        public EventHandlerGroup<T> After(params IEventHandler<T>[] handlers)
        {
            var selectedEventProcessors = new IEventProcessor[handlers.Length];
            for (int i = 0; i < handlers.Length; i++)
            {
                selectedEventProcessors[i] = _consumerRepository.GetEventProcessorFor(handlers[i]);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, selectedEventProcessors);
        }

        /// <summary>
        /// Create a group of <see cref="IEventProcessor"/>s to be used as a dependency.
        /// </summary>
        /// <param name="processors">the <see cref="IEventProcessor"/>s, previously set up with <see cref="HandleEventsWith(Disruptor.IEventProcessor[])"/>,
        ///                          that will form the <see cref="ISequenceBarrier"/> for subsequent <see cref="IEventHandler{T}"/> or <see cref="IEventProcessor"/>s.
        /// </param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a <see cref="ISequenceBarrier"/> over the specified <see cref="IEventProcessor"/>s.</returns>
        public EventHandlerGroup<T> After(params IEventProcessor[] processors)
        {
            foreach (var eventProcessor in processors)
            {
                _consumerRepository.Add(eventProcessor);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, processors);
        }

        /// <summary>
        /// Publish an event to the <see cref="RingBuffer"/>
        /// </summary>
        /// <param name="eventTranslator">the translator function that will load data into the event.</param>
        public void PublishEvent(Func<T, long, T> eventTranslator)
        {
            _eventPublisher.PublishEvent(eventTranslator);
        }

        /// <summary>
        /// Starts the <see cref="IEventProcessor"/>s and returns the fully configured <see cref="RingBuffer"/>.
        /// The <see cref="RingBuffer"/> is set up to prevent overwriting any entry that is yet to
        /// be processed by the slowest event processor.
        /// This method must only be called once after all <see cref="IEventProcessor"/>s have been added.
        /// </summary>
        /// <returns>the configured <see cref="RingBuffer"/>.</returns>
        public RingBuffer<T> Start()
        {
            var gatingSequences = _consumerRepository.GetLastSequenceInChain(true);
            _ringBuffer.SetGatingSequences(gatingSequences);

            CheckOnlyStartedOnce();
            foreach (var consumerInfo in _consumerRepository)
            {
                consumerInfo.Start(_executor);
            }

            return _ringBuffer;
        }

        /// <summary>
        /// Calls <see cref="IEventProcessor.Halt"/> on all the <see cref="IEventProcessor"/>s created via this <see cref="Disruptor{T}"/>.
        /// </summary>
        public void Halt()
        {
            foreach (var consumerInfo in _consumerRepository)
            {
                consumerInfo.Halt();
            }
        }

        /// <summary>
        /// Waits until all events currently in the <see cref="Disruptor"/> have been processed by all <see cref="IEventProcessor"/>s
        /// and then halts the <see cref="IEventProcessor"/>.  It is critical that publishing to the <see cref="RingBuffer"/> has stopped
        /// before calling this method, otherwise it may never return.
        /// </summary>
        public void Shutdown()
        {
            while (HasBacklog())
            {
                Thread.Sleep(0);
            }

            Halt();
        }

        /// <summary>
        /// The the <see cref="RingBuffer"/> used by this <see cref="Disruptor{T}"/>.  This is useful for creating custom
        /// <see cref="IEventProcessor"/> if the behaviour of <see cref="BatchEventProcessor{T}"/> is not suitable.
        /// </summary>
        public RingBuffer<T> RingBuffer
        {
            get { return _ringBuffer; }
        }

        /// <summary>
        /// Get the <see cref="ISequenceBarrier"/> used by a specific handler. Note that the <see cref="ISequenceBarrier"/>
        /// may be shared by multiple event handlers.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public ISequenceBarrier GetBarrierFor(IEventHandler<T> handler)
        {
            return _consumerRepository.GetBarrierFor(handler);
        }

        private bool HasBacklog()
        {
            var cursor = _ringBuffer.Cursor;
            var lastSequenceInChain = _consumerRepository.GetLastSequenceInChain(false);
            for (var i = 0; i < lastSequenceInChain.Length; i++)
            {
                if (cursor > lastSequenceInChain[i].Value)
                    return true;
            }
            return false;
        }

        internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barriereSequences, IEventHandler<T>[] eventHandlers)
        {
            CheckNotStarted();

            var processorSequences = new Sequence[eventHandlers.Length];
            var barrier = _ringBuffer.NewBarrier(barriereSequences);

            for (var i = 0; i < eventHandlers.Length; i++)
            {
                var eventHandler = eventHandlers[i];

                var batchEventProcessor = new BatchEventProcessor<T>(_ringBuffer, barrier, eventHandler);

                if (_exceptionHandler != null)
                {
                    batchEventProcessor.SetExceptionHandler(_exceptionHandler);
                }

                _consumerRepository.Add(batchEventProcessor, eventHandler, barrier);
                processorSequences[i] = batchEventProcessor.Sequence;
            }

            if (processorSequences.Length > 0)
            {
                _consumerRepository.UnMarkEventProcessorsAsEndOfChain(processorSequences);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
        }

        private void CheckNotStarted()
        {
            if (_running == 1)
            {
                throw new InvalidOperationException("All event handlers must be added before calling starts.");
            }
        }

        private void CheckOnlyStartedOnce()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                throw new InvalidOperationException("Disruptor.start() must only be called once.");
            }
        }
    }
}