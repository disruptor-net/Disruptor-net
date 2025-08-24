using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Processing;

/// <summary>
/// Utility type that encapsulates the running state of an <see cref="IEventProcessor"/>.
/// </summary>
/// <remarks>
/// The <see cref="EventProcessorState"/> is not exposed by the Disruptor API and if you are not implementing a custom event
/// processor, you likely do not need to use this type directly.
/// </remarks>
public class EventProcessorState
{
    private readonly object _lock = new();
    private readonly ICancellableBarrier _sequenceBarrier;
    private readonly bool _restartable;
    private bool _disposed;
    private RunState? _currentRunState;
    private Task _haltTask = Task.CompletedTask;
    private Task _disposeTask = Task.CompletedTask;

    public EventProcessorState(ICancellableBarrier sequenceBarrier, bool restartable)
    {
        _sequenceBarrier = sequenceBarrier;
        _restartable = restartable;
    }

    public bool IsRunning
    {
        get
        {
            lock (_lock)
            {
                var runState = _currentRunState;
                return runState is { IsShutdown: false };
            }
        }
    }

    public RunState Start()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                throw new InvalidOperationException("Event processor is disposed");
            }

            var runState = _currentRunState;
            if (runState != null)
            {
                if (!runState.IsShutdown)
                {
                    throw new InvalidOperationException("Event processor is already running");
                }

                if (!_restartable)
                {
                    throw new InvalidOperationException("Event processor was started once and cannot be restarted");
                }
            }

            _currentRunState = new RunState();
            return _currentRunState;
        }
    }

    public Task Halt()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return _disposeTask;
            }

            return HaltImpl();
        }
    }

    public Task Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return _disposeTask;
            }

            _disposed = true;

            var haltTask = HaltImpl();

            _disposeTask = _sequenceBarrier is IDisposable disposableBarrier
                ? haltTask.ContinueWith(_ => disposableBarrier.Dispose(), TaskContinuationOptions.ExecuteSynchronously)
                : haltTask;

            return _disposeTask;
        }
    }

    private Task HaltImpl()
    {
        var runState = _currentRunState;
        if (runState == null || runState.IsHalted)
        {
            return _haltTask;
        }

        runState.Halt();

        _sequenceBarrier.CancelProcessing();
        _haltTask = Task.WhenAll(_haltTask, runState.ShutdownTask);

        return _haltTask;
    }

    public class RunState
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();
#if NETSTANDARD
        private readonly TaskCompletionSource<object?> _startCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<object?> _shutdownCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
#else
        private readonly TaskCompletionSource _startCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _shutdownCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
#endif

        private volatile bool _isHalted;
        private volatile bool _isShutdown;

        public bool IsHalted => _isHalted;
        public bool IsShutdown => _isShutdown;
        public Task StartTask => _startCompletionSource.Task;
        public Task ShutdownTask => _shutdownCompletionSource.Task;
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public void Halt()
        {
            _isHalted = true;
            _cancellationTokenSource.Cancel();
        }

        public void OnStarted()
        {
#if NETSTANDARD
            _startCompletionSource.SetResult(null);
#else
            _startCompletionSource.SetResult();
#endif

        }

        public void OnShutdown()
        {
            _isShutdown = true;
#if NETSTANDARD
            _shutdownCompletionSource.SetResult(null);
#else
            _shutdownCompletionSource.SetResult();
#endif
        }
    }
}
