using System;

namespace Disruptor;

/// <summary>
/// Convenience implementation of an exception handler that using standard <c>Console.WriteLine</c> to log
/// the exception re-throw it wrapped in a <see cref="ApplicationException"/>
/// </summary>
public sealed class ValueFatalExceptionHandler<T> : IValueExceptionHandler<T>
    where T : struct
{
    public void HandleEventException(Exception ex, long sequence, ref T evt)
    {
        var message = $"Exception processing sequence {sequence} for event {evt}: {ex}";

        Console.WriteLine(message);

        throw new ApplicationException(message, ex);
    }

    public void HandleOnTimeoutException(Exception ex, long sequence)
    {
        var message = $"Exception during OnTimeout(): {ex}";

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
