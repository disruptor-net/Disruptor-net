using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
    /// and delegating the available events to a <see cref="IEventHandler{T}"/>.
    /// 
    /// If the <see cref="BatchEventProcessor{T}"/> also implements <see cref="ILifecycleAware"/> it will be notified just after the thread
    /// is started and just before the thread is shutdown.
    /// </summary>
    /// <typeparam name="T">Event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    internal sealed class BatchEventProcessor<T> : IEventProcessor where T : class
    {
        private const bool Running = true;
        private const bool Stopped = false;
        
        private Volatile.Boolean _running = new Volatile.Boolean(Stopped);
        private IExceptionHandler _exceptionHandler = new FatalExceptionHandler();
        private readonly RingBuffer<T> _ringBuffer;
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly IEventHandler<T> _eventHandler;
        private readonly Sequence _sequence = new Sequence(Sequencer.InitialCursorValue);

        /// <summary>
        /// Construct a <see cref="BatchEventProcessor{T}"/> that will automatically track the progress by updating its sequence when
        /// the <see cref="IEventHandler{T}.OnNext"/> method returns.
        /// </summary>
        /// <param name="ringBuffer">ringBuffer to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        public BatchEventProcessor(RingBuffer<T> ringBuffer, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        {
            _ringBuffer = ringBuffer;
            _sequenceBarrier = sequenceBarrier;
            _eventHandler = eventHandler;

            var sequenceReportingEventHandler = eventHandler as ISequenceReportingEventHandler<T>;
            if(sequenceReportingEventHandler != null)
            {
                sequenceReportingEventHandler.SetSequenceCallback(_sequence);
            }
        }

        public Sequence Sequence
        {
            get { return _sequence; }
        }

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.Alert"/> to notify the thread to check status.
        /// </summary>
        public void Halt()
        {
            _running.WriteFullFence(Stopped);
            _sequenceBarrier.Alert();
        }

        /// <summary>
        /// Set a new <see cref="IExceptionHandler"/> for handling exceptions propagated out of the <see cref="BatchEventProcessor{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        public void SetExceptionHandler(IExceptionHandler exceptionHandler)
        {
            if(exceptionHandler == null) throw new ArgumentNullException("exceptionHandler");

            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// It is ok to have another thread rerun this method after a halt().
        /// </summary>
        public void Run()
        {
            if (!_running.AtomicCompareExchange(Running, Stopped))
            {
                throw new InvalidOperationException("Thread is already running");
            }
            _sequenceBarrier.ClearAlert();
            
            NotifyStart();

            T evt = null;
            long nextSequence = _sequence.Value + 1L;
            while (true)
            {
                try
                {
                    long availableSequence = _sequenceBarrier.WaitFor(nextSequence);
                    while (nextSequence <= availableSequence)
                    {
                        evt = _ringBuffer[nextSequence];
                        _eventHandler.OnNext(evt, nextSequence, nextSequence == availableSequence);
                        nextSequence++;
                    }

                    _sequence.LazySet(nextSequence - 1L);
                }
                catch (AlertException)
                {
                    if (!_running.ReadFullFence())
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                    _sequence.LazySet(nextSequence);
                    nextSequence++;
                }
            }

            NotifyShutdown();

            _running.WriteFullFence(Stopped);
        }

        private void NotifyStart()
        {
            var lifecycleAware = _eventHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnStart();
                }
                catch (Exception e)
                {
                    _exceptionHandler.HandleOnStartException(e);
                }
            }
        }

        private void NotifyShutdown()
        {
            var lifecycleAware = _eventHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnShutdown();
                }
                catch (Exception e)
                {
                    _exceptionHandler.HandleOnShutdownException(e);
                }
            }
        }
    }
}