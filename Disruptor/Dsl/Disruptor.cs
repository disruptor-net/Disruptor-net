using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Dsl
{
    /// <summary>
    /// A DSL-style API for setting up the disruptor pattern around a ring buffer
    /// (aka the Builder pattern).
    /// 
    /// A simple example of setting up the disruptor with two event handlers that
    /// must process events in order:
    /// <code>Disruptor{MyEvent} disruptor = new Disruptor{MyEvent}(MyEvent.FACTORY, 32, Executors.NewCachedThreadPool());
    /// EventHandler{MyEvent} handler1 = new EventHandler{MyEvent}() { ... };
    /// EventHandler{MyEvent} handler2 = new EventHandler{MyEvent}() { ... };
    /// disruptor.HandleEventsWith(handler1);
    /// disruptor.After(handler1).HandleEventsWith(handler2);
    /// RingBuffer ringBuffer = disruptor.Start();</code>
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    public class Disruptor<T> where T : class
    {
        private readonly RingBuffer<T> _ringBuffer;
        private readonly IExecutor _executor;
        private readonly ConsumerRepository<T> _consumerRepository = new ConsumerRepository<T>();
        private IExceptionHandler<T> _exceptionHandler = new ExceptionHandlerWrapper<T>();
        private int _started;

        /// <summary>
        /// Create a new Disruptor. Will default to <see cref="BlockingWaitStrategy"/> and
        /// <see cref="ProducerType.Multi"/>
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer</param>
        /// <param name="ringBufferSize">the size of the ring buffer</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors</param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), new BasicExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Create a new Disruptor.
        /// </summary>
        /// <param name="eventFactory">the factory to create events in the ring buffer</param>
        /// <param name="ringBufferSize">the size of the ring buffer, must be power of 2</param>
        /// <param name="taskScheduler">a <see cref="TaskScheduler"/> to create threads to for processors</param>
        /// <param name="producerType">the claim strategy to use for the ring buffer</param>
        /// <param name="waitStrategy">the wait strategy to use for the ring buffer</param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, TaskScheduler taskScheduler, ProducerType producerType, IWaitStrategy waitStrategy)
            : this(RingBuffer<T>.Create(producerType, eventFactory, ringBufferSize, waitStrategy), new BasicExecutor(taskScheduler))
        {
        }

        /// <summary>
        /// Allows the executor to be specified
        /// </summary>
        /// <param name="eventFactory"></param>
        /// <param name="ringBufferSize"></param>
        /// <param name="executor"></param>
        public Disruptor(Func<T> eventFactory, int ringBufferSize, IExecutor executor)
            : this(RingBuffer<T>.CreateMultiProducer(eventFactory, ringBufferSize), executor)
        {
        }

        /// <summary>
        /// Private constructor helper
        /// </summary>
        private Disruptor(RingBuffer<T> ringBuffer, IExecutor executor)
        {
            _ringBuffer = ringBuffer;
            _executor = executor;
        }

        /// <summary>
        /// Set up event handlers to handle events from the ring buffer. These handlers will process events
        /// as soon as they become available, in parallel.
        /// <code>dw.HandleEventsWith(A).Then(B);</code>
        /// </summary>
        /// <param name="handlers">the event handlers that will process events</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventHandler<T>[] handlers)
        {
            return CreateEventProcessors(new Sequence[0], handlers);
        }

        /// <summary>
        /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
        /// automatically start these processors when <see cref="Start"/> is called.
        /// 
        /// This method can be used as the start of a chain. For example if the handler <code>A</code> must
        /// process events before handler<code>B</code>:
        /// <code>dw.HandleEventsWith(A).Then(B);</code>
        /// 
        /// Since this is the start of the chain, the processor factories will always be passed an empty <code>Sequence</code>
        /// array, so the factory isn't necessary in this case. This method is provided for consistency with
        /// <see cref="EventHandlerGroup{T}.HandleEventsWith(IEventProcessorFactory{T}[])"/> and <see cref="EventHandlerGroup{T}.Then(IEventProcessorFactory{T}[])"/>
        /// which do have barrier sequences to provide.
        /// </summary>
        /// <param name="eventProcessorFactories">eventProcessorFactories the event processor factories to use to create the event processors that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventProcessorFactory<T>[] eventProcessorFactories)
        {
            return CreateEventProcessors(new Sequence[0], eventProcessorFactories);
        }
        
        /// <summary>
        /// Set up custom event processors to handle events from the ring buffer. The Disruptor will
        /// automatically start this processors when <see cref="Start"/> is called.
        /// 
        /// This method can be used as the start of a chain. For example if the processor <code>A</code> must
        /// process events before handler<code>B</code>:
        /// <code>dw.HandleEventsWith(A).Then(B);</code>
        /// </summary>
        /// <param name="processors">processors the event processors that will process events</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWith(params IEventProcessor[] processors)
        {
            foreach (var processor in processors)
            {
                _consumerRepository.Add(processor);
            }
            return new EventHandlerGroup<T>(this, _consumerRepository, Util.GetSequencesFor(processors));
        }
        
        /// <summary>
        /// Set up a <see cref="WorkerPool{T}"/> to distribute an event to one of a pool of work handler threads.
        /// Each event will only be processed by one of the work handlers.
        /// The Disruptor will automatically start this processors when <see cref="Start"/> is called.
        /// </summary>
        /// <param name="workHandlers">the work handlers that will process events.</param>
        /// <returns>a <see cref="EventHandlerGroup{T}"/> that can be used to chain dependencies.</returns>
        public EventHandlerGroup<T> HandleEventsWithWorkerPool(params IWorkHandler<T>[] workHandlers)
        {
            return CreateWorkerPool(new Sequence[0], workHandlers);
        }

        /// <summary>
        /// Specify an exception handler to be used for any future event handlers.
        /// Note that only event handlers set up after calling this method will use the exception handler.
        /// </summary>
        /// <param name="exceptionHandler">the exception handler to use for any future <see cref="IEventProcessor"/>.</param>
        [Obsolete("This method only applies to future event handlers. Use setDefaultExceptionHandler instead which applies to existing and new event handlers.")]
        public void HandleExceptionsWith(IExceptionHandler<object> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// Specify an exception handler to be used for event handlers and worker pools created by this Disruptor.
        /// The exception handler will be used by existing and future event handlers and worker pools created by this Disruptor instance.
        /// </summary>
        /// <param name="exceptionHandler">the exception handler to use</param>
        public void SetDefaultExceptionHandler(IExceptionHandler<T> exceptionHandler)
        {
            CheckNotStarted();
            ((ExceptionHandlerWrapper<T>)_exceptionHandler).SwitchTo(exceptionHandler);
        }
        
        /// <summary>
        /// Override the default exception handler for a specific handler.
        /// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
        /// </summary>
        /// <param name="eventHandler">eventHandler the event handler to set a different exception handler for</param>
        /// <returns>an <see cref="ExceptionHandlerSetting{T}"/> dsl object - intended to be used by chaining the with method call</returns>
        public ExceptionHandlerSetting<T> HandleExceptionsFor(IEventHandler<T> eventHandler)
        {
            return new ExceptionHandlerSetting<T>(eventHandler, _consumerRepository);
        }

        /// <summary>
        /// Create a group of event handlers to be used as a dependency.
        /// For example if the handler <code>A</code> must process events before handler <code>B</code>:
        /// <code>dw.After(A).HandleEventsWith(B);</code>
        /// </summary>
        /// <param name="handlers">handlers the event handlers, previously set up with {@link #handleEventsWith(com.lmax.disruptor.EventHandler[])},
        /// that will form the barrier for subsequent handlers or processors.</param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a dependency barrier over the specified event handlers.</returns>
        public EventHandlerGroup<T> After(params IEventHandler<T>[] handlers)
        {
            return new EventHandlerGroup<T>(this, _consumerRepository, handlers.Select(h => _consumerRepository.GetSequenceFor(h)));
        }

        /// <summary>
        /// Create a group of event processors to be used as a dependency.
        /// </summary>
        /// <see cref="After(Disruptor.IEventHandler{T}[])"/>
        /// <param name="processors">processors the event processors, previously set up with <see cref="HandleEventsWith(Disruptor.IEventHandler{T}[])"/>,
        /// that will form the barrier for subsequent handlers or processors.</param>
        /// <returns>an <see cref="EventHandlerGroup{T}"/> that can be used to setup a <see cref="ISequenceBarrier"/> over the specified event processors.</returns>
        public EventHandlerGroup<T> After(params IEventProcessor[] processors)
        {
            foreach (var processor in processors)
            {
                _consumerRepository.Add(processor);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, Util.GetSequencesFor(processors));
        }

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <param name="eventTranslator">the translator that will load data into the event</param>
        public void PublishEvent(IEventTranslator<T> eventTranslator) => _ringBuffer.PublishEvent(eventTranslator);

        /// <summary>
        /// Publish an event to the ring buffer.
        /// </summary>
        /// <param name="eventTranslator">the translator that will load data into the event</param>
        /// <param name="arg">A single argument to load into the event</param>
        public void PublishEvent<A>(IEventTranslatorOneArg<T, A> eventTranslator, A arg) => _ringBuffer.PublishEvent(eventTranslator, arg);

        /// <summary>
        /// Publish a batch of events to the ring buffer.
        /// </summary>
        /// <param name="eventTranslator">the translator that will load data into the event</param>
        /// <param name="arg">An array single arguments to load into the events. One Per event.</param>
        public void PublishEvent<A>(IEventTranslatorOneArg<T, A> eventTranslator, A[] arg) => _ringBuffer.PublishEvents(eventTranslator, arg);

        /// <summary>
        /// Starts the event processors and returns the fully configured ring buffer.
        /// 
        /// The ring buffer is set up to prevent overwriting any entry that is yet to
        /// be processed by the slowest event processor.
        /// 
        /// This method must only be called once after all event processors have been added.
        /// </summary>
        /// <returns>the configured ring buffer</returns>
        public RingBuffer<T> Start()
        {
            _ringBuffer.AddGatingSequences(_consumerRepository.GetLastSequenceInChain(true));

            CheckOnlyStartedOnce();
            foreach (var consumerInfo in _consumerRepository)
            {
                consumerInfo.Start(_executor);
            }

            return _ringBuffer;
        }

        /// <summary>
        /// Calls <see cref="IEventProcessor.Halt"/> on all of the event processors created via this disruptor.
        /// </summary>
        public void Halt()
        {
            foreach (var consumerInfo in _consumerRepository)
            {
                consumerInfo.Halt();
            }
        }

        /// <summary>
        /// Waits until all events currently in the disruptor have been processed by all event processors
        /// and then halts the processors.It is critical that publishing to the ring buffer has stopped
        /// before calling this method, otherwise it may never return.
        /// 
        /// This method will not shutdown the executor, nor will it await the final termination of the
        /// processor threads
        /// </summary>
        public void Shutdown()
        {
            try
            {
                Shutdown(TimeSpan.MinValue); // do not wait
            }
            catch (TimeoutException e)
            {
                _exceptionHandler.HandleOnShutdownException(e);
            }
        }

        /// <summary>
        /// Waits until all events currently in the disruptor have been processed by all event processors
        /// and then halts the processors.
        /// 
        /// This method will not shutdown the executor, nor will it await the final termination of the
        /// processor threads
        /// </summary>
        /// <param name="timeout">the amount of time to wait for all events to be processed. <code>TimeSpan.MaxValue</code> will give an infinite timeout</param>
        public void Shutdown(TimeSpan timeout)
        {
            var timeoutAt = DateTime.UtcNow.Add(timeout);
            while (HasBacklog())
            {
                if (timeout.Ticks >= 0 && DateTime.UtcNow > timeoutAt)
                    throw TimeoutException.Instance;

                // Busy spin
            }
            Halt();
        }

        /// <summary>
        /// The <see cref="RingBuffer{T}"/> used by this Disruptor. This is useful for creating custom
        /// event processors if the behaviour of <see cref="BatchEventProcessor{T}"/> is not suitable.
        /// </summary>
        public RingBuffer<T> RingBuffer => _ringBuffer;

        /// <summary>
        /// Get the value of the cursor indicating the published sequence.
        /// </summary>
        public long Cursor => _ringBuffer.Cursor;

        /// <summary>
        /// The capacity of the data structure to hold entries.
        /// </summary>
        public long BufferSize => _ringBuffer.BufferSize;

        /// <summary>
        /// Get the event for a given sequence in the RingBuffer.
        /// <see cref="RingBuffer{T}.this"/>
        /// </summary>
        /// <param name="sequence">sequence for the event</param>
        /// <returns>event for the sequence</returns>
        public T this[long sequence] => _ringBuffer[sequence];

        /// <summary>
        /// Get the <see cref="ISequenceBarrier"/> used by a specific handler. Note that the <see cref="ISequenceBarrier"/>
        /// may be shared by multiple event handlers.
        /// </summary>
        /// <param name="handler">the handler to get the barrier for</param>
        /// <returns>the SequenceBarrier used by the given handler</returns>
        public ISequenceBarrier GetBarrierFor(IEventHandler<T> handler) => _consumerRepository.GetBarrierFor(handler);

        // Confirms if all messages have been consumed by all event processors
        private bool HasBacklog()
        {
            var cursor = _ringBuffer.Cursor;
            foreach (var sequence in _consumerRepository.GetLastSequenceInChain(false))
            {
                if (cursor > sequence.Value)
                    return true;
            }
            return false;
        }

        internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IEventHandler<T>[] eventHandlers)
        {
            CheckNotStarted();

            var processorSequences = new List<Sequence>(eventHandlers.Length);
            var barrier = _ringBuffer.NewBarrier(barrierSequences);

            foreach (var eventHandler in eventHandlers)
            {
                var batchEventProcessor = new BatchEventProcessor<T>(_ringBuffer, barrier, eventHandler);
                batchEventProcessor.SetExceptionHandler(_exceptionHandler);

                _consumerRepository.Add(batchEventProcessor, eventHandler, barrier);
                processorSequences.Add(batchEventProcessor.Sequence);
            }

            if (processorSequences.Count > 0)
            {
                _consumerRepository.UnMarkEventProcessorsAsEndOfChain(barrierSequences);
            }

            return new EventHandlerGroup<T>(this, _consumerRepository, processorSequences);
        }

        internal EventHandlerGroup<T> CreateEventProcessors(Sequence[] barrierSequences, IEventProcessorFactory<T>[] processorFactories)
        {
            return HandleEventsWith(processorFactories.Select(p => p.CreateEventProcessor(_ringBuffer, barrierSequences)).ToArray());
        }

        internal EventHandlerGroup<T> CreateWorkerPool(Sequence[] barrierSequences, IWorkHandler<T>[] workHandlers)
        {
            var sequenceBarrier = _ringBuffer.NewBarrier(barrierSequences);
            var workerPool = new WorkerPool<T>(_ringBuffer, sequenceBarrier, _exceptionHandler, workHandlers);
            _consumerRepository.Add(workerPool, sequenceBarrier);

            return new EventHandlerGroup<T>(this, _consumerRepository, workerPool.WorkerSequences);
        }

        private void CheckNotStarted()
        {
            if (_started == 1)
            {
                throw new InvalidOperationException("All event handlers must be added before calling starts.");
            }
        }

        private void CheckOnlyStartedOnce()
        {
            if (Interlocked.Exchange(ref _started, 1) != 0)
            {
                throw new InvalidOperationException("Disruptor.start() must only be called once.");
            }
        }
    }
}