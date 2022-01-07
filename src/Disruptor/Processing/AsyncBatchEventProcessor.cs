using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Util;

#if DISRUPTOR_V5

namespace Disruptor.Processing
{
    /// <summary>
    /// Convenience class for handling the batching semantics of consuming events from a <see cref="RingBuffer{T}"/>
    /// and delegating the available events to an <see cref="IBatchEventHandler{T}"/>.
    ///
    /// If the <see cref="IBatchEventHandler{T}"/> also implements <see cref="ILifecycleAware"/> it will be notified just after the thread
    /// is started and just before the thread is shutdown.
    /// </summary>
    /// <remarks>
    /// You should probably not use this type directly but instead implement <see cref="IBatchEventHandler{T}"/> and register your handler
    /// using <see cref="Disruptor{T}.HandleEventsWith(IBatchEventHandler{T}[])"/>.
    /// </remarks>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <typeparam name="TDataProvider">the type of the <see cref="IDataProvider{T}"/> used.</typeparam>
    /// <typeparam name="TSequenceBarrier">the type of the <see cref="ISequenceBarrier"/> used.</typeparam>
    /// <typeparam name="TEventHandler">the type of the <see cref="IBatchEventHandler{T}"/> used.</typeparam>
    public class AsyncBatchEventProcessor<T, TDataProvider, TSequenceBarrier, TEventHandler> : IAsyncEventProcessor<T>
        where T : class
        where TDataProvider : IDataProvider<T>
        where TSequenceBarrier : IAsyncSequenceBarrier
        where TEventHandler : IAsyncBatchEventHandler<T>
    {
        // ReSharper disable FieldCanBeMadeReadOnly.Local (performance: the runtime type will be a struct)
        private TDataProvider _dataProvider;
        private TSequenceBarrier _sequenceBarrier;
        private TEventHandler _eventHandler;
        // ReSharper restore FieldCanBeMadeReadOnly.Local

        private readonly Sequence _sequence = new Sequence();
        private readonly ITimeoutHandler? _timeoutHandler;
        private readonly ManualResetEventSlim _started = new ManualResetEventSlim();
        private IExceptionHandler<T> _exceptionHandler = new FatalExceptionHandler<T>();
        private volatile int _runState = ProcessorRunStates.Idle;

        public AsyncBatchEventProcessor(TDataProvider dataProvider, TSequenceBarrier sequenceBarrier, TEventHandler eventHandler)
        {
            _dataProvider = dataProvider;
            _sequenceBarrier = sequenceBarrier;
            _eventHandler = eventHandler;

            if (eventHandler is IEventProcessorSequenceAware sequenceAware)
                sequenceAware.SetSequenceCallback(_sequence);

            _timeoutHandler = eventHandler as ITimeoutHandler;
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
            _runState = ProcessorRunStates.Halted;
            _sequenceBarrier.CancelProcessing();
        }

        /// <summary>
        /// <see cref="IEventProcessor.IsRunning"/>
        /// </summary>
        public bool IsRunning => _runState != ProcessorRunStates.Idle;

        /// <summary>
        /// Set a new <see cref="IExceptionHandler{T}"/> for handling exceptions propagated out of the <see cref="IBatchEventHandler{T}"/>
        /// </summary>
        /// <param name="exceptionHandler">exceptionHandler to replace the existing exceptionHandler.</param>
        public void SetExceptionHandler(IExceptionHandler<T> exceptionHandler)
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

        public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
        {
            return Task.Factory.StartNew(async () => await RunAsync(), CancellationToken.None, TaskCreationOptions.None, taskScheduler).Unwrap();
        }

        /// <summary>
        /// It is ok to have another thread rerun this method after a halt().
        /// </summary>
        /// <exception cref="InvalidOperationException">if this object instance is already running in a thread</exception>
        public async Task RunAsync()
        {
#pragma warning disable 420
            var previousRunning = Interlocked.CompareExchange(ref _runState, ProcessorRunStates.Running, ProcessorRunStates.Idle);
#pragma warning restore 420

            if (previousRunning == ProcessorRunStates.Running)
            {
                throw new InvalidOperationException("Thread is already running");
            }

            if (previousRunning == ProcessorRunStates.Idle)
            {
                _sequenceBarrier.ResetProcessing();

                NotifyStart();
                try
                {
                    if (_runState == ProcessorRunStates.Running)
                    {
                        await ProcessEvents();
                    }
                }
                finally
                {
                    NotifyShutdown();
                    _runState = ProcessorRunStates.Idle;
                }
            }
            else
            {
                EarlyExit();
            }
        }

        [MethodImpl(Constants.AggressiveOptimization)]
        private async Task ProcessEvents()
        {
            var nextSequence = _sequence.Value + 1L;
            var availableSequence = _sequence.Value;

            while (true)
            {
                try
                {
                    var waitResult = await _sequenceBarrier.WaitForAsync(nextSequence);
                    if (waitResult.IsTimeout)
                    {
                        NotifyTimeout(_sequence.Value);
                        continue;
                    }

                    availableSequence = waitResult.UnsafeAvailableSequence;
                    if (availableSequence >= nextSequence)
                    {
                        var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                        await _eventHandler.OnBatch(batch, nextSequence);
                        nextSequence += batch.Length;
                    }

                    _sequence.SetValue(nextSequence - 1);
                }
                catch (OperationCanceledException) when (_sequenceBarrier.IsCancellationRequested())
                {
                    if (_runState != ProcessorRunStates.Running)
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    if (availableSequence >= nextSequence)
                    {
                        var batch = _dataProvider.GetBatch(nextSequence, availableSequence);
                        _exceptionHandler.HandleEventException(ex, nextSequence, batch);
                        nextSequence += batch.Length;
                        _sequence.SetValue(nextSequence - 1);
                    }
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
                _timeoutHandler?.OnTimeout(availableSequence);
            }
            catch (Exception ex)
            {
                _exceptionHandler.HandleEventException(ex, availableSequence, null);
            }
        }

        /// <summary>
        /// Notifies the EventHandler when this processor is starting up
        /// </summary>
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

        /// <summary>
        /// Notifies the EventHandler immediately prior to this processor shutting down
        /// </summary>
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
    }
}

#endif
