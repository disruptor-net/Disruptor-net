using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Benchmarks.Reference
{
   public class ValueBatchEventProcessorRef<T, TDataProvider, TSequenceBarrier, TEventHandler> : IValueEventProcessor<T>
        where T : struct

        where TDataProvider : IValueDataProvider<T>
        where TSequenceBarrier : ISequenceBarrier
        where TEventHandler : IValueEventHandler<T>
   {
        private static class RunningStates
        {
            public const int Idle = 0;
            public const int Halted = Idle + 1;
            public const int Running = Halted + 1;
        }

        // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TDataProvider _dataProvider;
        private TSequenceBarrier _sequenceBarrier;
        private TEventHandler _eventHandler;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private readonly Sequence _sequence = new Sequence();
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim();
        private IValueExceptionHandler<T> _exceptionHandler = new ValueFatalExceptionHandler<T>();
        private volatile int _running;

        /// <summary>
        /// Construct a BatchEventProcessor that will automatically track the progress by updating its sequence when
        /// the <see cref="IValueEventHandler{T}.OnEvent"/> method returns.
        ///
        /// Consider using <see cref="EventProcessorFactory"/> to create your <see cref="IEventProcessor"/>.
        /// </summary>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        /// <param name="batchStartAware"></param>
        public ValueBatchEventProcessorRef(TDataProvider dataProvider, TSequenceBarrier sequenceBarrier, TEventHandler eventHandler)
        {
            _dataProvider = dataProvider;
            _sequenceBarrier = sequenceBarrier;
            _eventHandler = eventHandler;

            if (eventHandler is IEventProcessorSequenceAware sequenceAware)
                sequenceAware.SetSequenceCallback(_sequence);
        }

        /// <summary>
        /// <see cref="IEventProcessor.Sequence"/>
        /// </summary>
        public ISequence Sequence => _sequence;

        /// <summary>
        /// Signal that this <see cref="IEventProcessor"/> should stop when it has finished consuming at the next clean break.
        /// It will call <see cref="ISequenceBarrier.CancelProcessing"/> to notify the thread to check status.
        /// </summary>
        public void Halt()
        {
            _running = RunningStates.Halted;
            _sequenceBarrier.CancelProcessing();
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
        public bool IsRunning => _running != RunningStates.Idle;

        /// <summary>
        /// Set a new <see cref="IValueExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IValueEventHandler{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        public void SetExceptionHandler(IValueExceptionHandler<T> exceptionHandler)
        {
            _exceptionHandler = exceptionHandler ?? throw new ArgumentNullException(nameof(exceptionHandler));
        }

        /// <summary>
        /// Waits before the event processor enters the <see cref="IsRunning"/> state.
        /// </summary>
        /// <param name="timeout">maximum wait duration</param>
        public void WaitUntilStarted(TimeSpan timeout)
        {
            _started.Wait(timeout);
        }

        /// <summary>
        /// <inheritdoc cref="IEventProcessor.Start"/>.
        /// </summary>
        public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
        {
            return taskScheduler.ScheduleAndStart(Run, taskCreationOptions);
        }

        /// <summary>
        /// It is ok to have another thread rerun this method after a halt().
        /// </summary>
        /// <exception cref="InvalidOperationException">if this object instance is already running in a thread</exception>
        public void Run()
        {
#pragma warning disable 420
            var previousRunning = Interlocked.CompareExchange(ref _running, RunningStates.Running, RunningStates.Idle);
#pragma warning restore 420

            if (previousRunning == RunningStates.Running)
            {
                throw new InvalidOperationException("Thread is already running");
            }

            if (previousRunning == RunningStates.Idle)
            {
                _sequenceBarrier.ResetProcessing();

                NotifyStart();
                try
                {
                    if (_running == RunningStates.Running)
                    {
                        ProcessEvents();
                    }
                }
                finally
                {
                    NotifyShutdown();
                    _running = RunningStates.Idle;
                }
            }
            else
            {
                EarlyExit();
            }
        }

        [MethodImpl(Constants.AggressiveOptimization)]
        private void ProcessEvents()
        {
            var nextSequence = _sequence.Value + 1L;

            while (true)
            {
                try
                {
                    var waitResult = _sequenceBarrier.WaitFor(nextSequence);
                    if (waitResult.IsTimeout)
                    {
                        NotifyTimeout(_sequence.Value);
                        continue;
                    }

                    var availableSequence = waitResult.UnsafeAvailableSequence;

                    _eventHandler.OnBatchStart(availableSequence - nextSequence + 1);

                    while (nextSequence <= availableSequence)
                    {
                        ref T evt = ref _dataProvider[nextSequence];
                        _eventHandler.OnEvent(ref evt, nextSequence, nextSequence == availableSequence);
                        nextSequence++;
                    }

                    _sequence.SetValue(availableSequence);
                }
                catch (OperationCanceledException) when (_sequenceBarrier.IsCancellationRequested())
                {
                    if (_running != RunningStates.Running)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    ref T evt = ref _dataProvider[nextSequence];

                    _exceptionHandler.HandleEventException(ex, nextSequence, ref evt);
                    _sequence.SetValue(nextSequence);
                    nextSequence++;
                }
            }
        }

        private void EarlyExit()
        {
            NotifyStart();
            NotifyShutdown();
        }

        private void NotifyTimeout(long availableSequence)
        {
            try
            {
                _eventHandler.OnTimeout(availableSequence);
            }
            catch (Exception ex)
            {
                _exceptionHandler.HandleOnTimeoutException(ex, availableSequence);
            }
        }

        /// <summary>
        /// Notifies the EventHandler when this processor is starting up
        /// </summary>
        private void NotifyStart()
        {
            try
            {
                _eventHandler.OnStart();
            }
            catch (Exception e)
            {
                _exceptionHandler.HandleOnStartException(e);
            }

            _started.Set();
        }

        /// <summary>
        /// Notifies the EventHandler immediately prior to this processor shutting down
        /// </summary>
        private void NotifyShutdown()
        {
            try
            {
                _eventHandler.OnShutdown();
            }
            catch (Exception e)
            {
                _exceptionHandler.HandleOnShutdownException(e);
            }

            _started.Reset();
        }
    }
}
