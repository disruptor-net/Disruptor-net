using System;

namespace Disruptor;

/// <summary>
/// Convenience implementation of an exception handler that using Console.WriteLine to log the exception
/// </summary>
public class IgnoreExceptionHandler<T> : IExceptionHandler<T>
    where T : class
{
    public void HandleEventException(Exception ex, long sequence, T evt)
    {
        var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

        Console.WriteLine(message);
    }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        var message = $"Exception processing timeout for sequence {sequence}: {ex}";

        Console.WriteLine(message);
    }

    public void HandleEventException(Exception ex, long sequence, EventBatch<T> batch)
    {
        var message = $"Exception processing sequence {sequence} for batch of {batch.Length} events, first event {batch[0]}: {ex}";

        Console.WriteLine(message);
    }

    public void HandleOnStartException(Exception ex)
    {
        var message = $"Exception during OnStart(): {ex}";

        Console.WriteLine(message);
    }

    public void HandleOnShutdownException(Exception ex)
    {
        var message = $"Exception during OnShutdown(): {ex}";

        Console.WriteLine(message);
    }
}