using System;
using System.IO;

namespace Disruptor;

/// <summary>
/// Convenience implementation of an exception handler that uses <c>Console.WriteLine</c> to log
/// the exception and re-throw it wrapped in a <see cref="ApplicationException"/>
/// </summary>
public sealed class FatalExceptionHandler<T> : IExceptionHandler<T>
    where T : class
{
    private readonly TextWriter _log;

    public FatalExceptionHandler()
        : this(Console.Out)
    {
    }

    public FatalExceptionHandler(TextWriter log)
    {
        _log = log;
    }

    public void HandleEventException(Exception ex, long sequence, T evt)
    {
        var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

        _log.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        var message = $"Exception processing timeout for sequence {sequence}: {ex}";

        _log.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
    {
        var message = $"Exception processing sequence {sequence} for batch of {batch.Length} events, first event {batch[0]}: {ex}";

        _log.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnStartException(Exception ex)
    {
        var message = $"Exception during OnStart(): {ex}";

        _log.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnShutdownException(Exception ex)
    {
        var message = $"Exception during OnShutdown(): {ex}";

        _log.WriteLine(message);

        throw new ApplicationException(message, ex);
    }
}
