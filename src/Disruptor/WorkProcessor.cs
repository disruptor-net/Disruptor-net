using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// A <see cref="WorkProcessor{T}"/> wraps a single <see cref="IWorkHandler{T}"/>, effectively consuming the sequence and ensuring appropriate barriers.
    ///
    /// Generally, this will be used as part of a <see cref="WorkerPool{T}"/>.
    /// </summary>
    /// <typeparam name="T">event implementation storing the details for the work to processed.</typeparam>
    public sealed class WorkProcessor<T> : IEventProcessor where T : class
    {
        private volatile int _running;
        private readonly Sequence _sequence = new Sequence();
        private readonly RingBuffer<T> _ringBuffer;
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly IWorkHandler<T> _workHandler;
        private readonly IExceptionHandler<T> _exceptionHandler;
        private readonly ISequence _workSequence;
        private readonly IEventReleaser _eventReleaser;
        private readonly ITimeoutHandler _timeoutHandler;

        /// <summary>
        /// Construct a <see cref="WorkProcessor{T}"/>.
        /// </summary>
        /// <param name="ringBuffer">ringBuffer to which events are published.</param>
        /// <param name="sequenceBarrier">sequenceBarrier on which it is waiting.</param>
        /// <param name="workHandler">workHandler is the delegate to which events are dispatched.</param>
        /// <param name="exceptionHandler">exceptionHandler to be called back when an error occurs</param>
        /// <param name="workSequence">workSequence from which to claim the next event to be worked on.  It should always be initialised
        /// as <see cref="Disruptor.Sequence.InitialCursorValue"/></param>
        public WorkProcessor(RingBuffer<T> ringBuffer, ISequenceBarrier sequenceBarrier, IWorkHandler<T> workHandler, IExceptionHandler<T> exceptionHandler, ISequence workSequence)
        {
            _ringBuffer = ringBuffer;
            _sequenceBarrier = sequenceBarrier;
            _workHandler = workHandler;
            _exceptionHandler = exceptionHandler;
            _workSequence = workSequence;
            _eventReleaser = new EventReleaser(this);

            (_workHandler as IEventReleaseAware)?.SetEventReleaser(_eventReleaser);
            _timeoutHandler = _workHandler as ITimeoutHandler;
        }

        /// <summary>
        /// Return a reference to the <see cref="IEventProcessor.Sequence"/> being used by this <see cref="IEventProcessor"/>
        /// </summary>
        public ISequence Sequence => _sequence;

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.Alert"/> to notify the thread to check status.
        /// </summary>
        public void Halt()
        {
            _running = 0;
            _sequenceBarrier.Alert();
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
        public bool IsRunning => _running == 1;

        /// <summary>
        /// It is ok to have another thread re-run this method after a halt().
        /// </summary>
        [MethodImpl(Constants.AggressiveOptimization)]
        public void Run()
        {
            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                throw new InvalidOperationException("Thread is already running");
            }
            _sequenceBarrier.ClearAlert();

            NotifyStart();

            var processedSequence = true;
            var cachedAvailableSequence = long.MinValue;
            var nextSequence = _sequence.Value;
            T eventRef = null;
            while (true)
            {
                try
                {
                    if (processedSequence)
                    {
                        processedSequence = false;
                        do
                        {
                            nextSequence = _workSequence.Value + 1L;
                            _sequence.SetValue(nextSequence - 1L);
                        } while (!_workSequence.CompareAndSet(nextSequence - 1L, nextSequence));
                    }

                    if (cachedAvailableSequence >= nextSequence)
                    {
                        eventRef = _ringBuffer[nextSequence];
                        _workHandler.OnEvent(eventRef);
                        processedSequence = true;
                    }
                    else
                    {
                        cachedAvailableSequence = _sequenceBarrier.WaitFor(nextSequence);
                    }
                }
                catch (TimeoutException)
                {
                    NotifyTimeout(_sequence.Value);
                }
                catch (AlertException)
                {
                    if (_running == 0)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    _exceptionHandler.HandleEventException(ex, nextSequence, eventRef);
                    processedSequence = true;
                }
            }

            NotifyShutdown();

            _running = 0;
        }

        private void NotifyTimeout(long availableSequence)
        {
            try
            {
                _timeoutHandler?.OnTimeout(availableSequence);
            }
            catch (Exception ex)
            {
                _exceptionHandler.HandleEventException(ex, availableSequence, null);
            }
        }

        private void NotifyStart()
        {
            var lifecycleAware = _workHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnStart();
                }
                catch (Exception ex)
                {
                    _exceptionHandler.HandleOnStartException(ex);
                }
            }
        }

        private void NotifyShutdown()
        {
            var lifecycleAware = _workHandler as ILifecycleAware;
            if (lifecycleAware != null)
            {
                try
                {
                    lifecycleAware.OnShutdown();
                }
                catch (Exception ex)
                {
                    _exceptionHandler.HandleOnShutdownException(ex);
                }
            }
        }

        private class EventReleaser : IEventReleaser
        {
            private readonly WorkProcessor<T> _workProcessor;

            public EventReleaser(WorkProcessor<T> workProcessor)
            {
                _workProcessor = workProcessor;
            }

            public void Release()
            {
                _workProcessor._sequence.SetValue(long.MaxValue);
            }
        }
    }
}
