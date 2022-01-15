using System;

namespace Disruptor;

/// <summary>
/// Convenience implementation of an exception handler that using standard Console.Writeline to log
/// the exception re-throw it wrapped in a <see cref="ApplicationException"/>
/// </summary>
public sealed class FatalExceptionHandler<T> : IExceptionHandler<T>
    where T : class
{
    public void HandleEventException(Exception ex, long sequence, T evt)
    {
        var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        var message = $"Exception processing timeout for sequence {sequence}: {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
    {
        var message = $"Exception processing sequence {sequence} for batch of {batch.Length} events, first event {batch[0]}: {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnStartException(Exception ex)
    {
        var message = $"Exception during OnStart(): {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnShutdownException(Exception ex)
    {
        var message = $"Exception during OnShutdown(): {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }
}