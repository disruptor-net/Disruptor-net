using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
    /// and delegating the available events to an <see cref="IEventHandler{T}"/>.
    /// 
    /// If the <see cref="BatchEventProcessor{T}"/> also implements <see cref="ILifecycleAware"/> it will be notified just after the thread
    /// is started and just before the thread is shutdown.
    /// </summary>
    /// <typeparam name="T">Event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    public sealed class BatchEventProcessor<T> : IEventProcessor where T : class
    {
        private static class RunningStates
        {
            public const int Idle = 0;
            public const int Halted = Idle + 1;
            public const int Running = Halted + 1;
        }

        private readonly IDataProvider<T> _dataProvider;
        private readonly ISequenceBarrier _sequenceBarrier;
        private readonly IEventHandler<T> _eventHandler;
        private readonly Sequence _sequence = new Sequence();
        private readonly IBatchStartAware _batchStartAware;
        private readonly ITimeoutHandler _timeoutHandler;
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim();
        private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler();
        private volatile int _running;

        /// <summary>
        /// Construct a <see cref="BatchEventProcessor{T}"/> that will automatically track the progress by updating its sequence when
        /// the <see cref="IEventHandler{T}.OnEvent"/> method returns.
        /// </summary>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        public BatchEventProcessor(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        {
            _dataProvider = dataProvider;
            _sequenceBarrier = sequenceBarrier;
            _eventHandler = eventHandler;

            if (eventHandler is ISequenceReportingEventHandler<T> sequenceReportingEventHandler)
                sequenceReportingEventHandler.SetSequenceCallback(_sequence);

            _batchStartAware = eventHandler as IBatchStartAware;
            _timeoutHandler = eventHandler as ITimeoutHandler;
        }

        /// <summary>
        /// <see cref="IEventProcessor.Sequence"/>
        /// </summary>
        public ISequence Sequence => _sequence;

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.Alert"/> to notify the thread to check status.
        /// </summary>
        public void Halt()
        {
            _running = RunningStates.Halted;
            _sequenceBarrier.Alert();
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
        public bool IsRunning => _running != RunningStates.Idle;

        /// <summary>
        /// Set a new <see cref="IExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="BatchEventProcessor{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        public void SetExceptionHandler(IExceptionHandler<T> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        }

        /// <summary>
        /// It is ok to have another thread rerun this method after a halt().
        /// </summary>
        public void Run()
        {
#pragma warning disable 420
            if (Interlocked.CompareExchange(ref _running, RunningStates.Running, RunningStates.Idle) == RunningStates.Running)
#pragma warning restore 420
            {
                throw new InvalidOperationException("Thread is already running");
            }
            _sequenceBarrier.ClearAlert();

            NotifyStart();

            try
            {
                if (_running == RunningStates.Halted)
                {
                    return;
                }

                T evt = null;
                var nextSequence = _sequence.Value + 1L;

                while (true)
                {
                    try
                    {
                        var availableSequence = _sequenceBarrier.WaitFor(nextSequence);

                        if (_batchStartAware != null)
                        {
                            _batchStartAware.OnBatchStart(availableSequence - nextSequence + 1);
                        }

                        while (nextSequence <= availableSequence)
                        {
                            evt = _dataProvider[nextSequence];
                            _eventHandler.OnEvent(evt, nextSequence, nextSequence == availableSequence);
                            nextSequence++;
                        }

                        _sequence.SetValue(availableSequence);
                    }
                    catch (TimeoutException)
                    {
                        NotifyTimeout(_sequence.Value);
                    }
                    catch (AlertException)
                    {
                        if (_running != RunningStates.Running)
                        {
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _exceptionHandler.HandleEventException(ex, nextSequence, evt);
                        _sequence.SetValue(nextSequence);
                        nextSequence++;
                    }
                }
            }
            finally
            {
                NotifyShutdown();
                _running = RunningStates.Idle;
            }
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
            
            _started.Set();
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

            _started.Reset();
        }

        internal void WaitUntilStarted(TimeSpan timeout)
        {
            _started.Wait(timeout);
        }
    }
}