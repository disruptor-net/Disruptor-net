using Disruptor.Processing;

namespace Disruptor.Dsl;

/// <summary>
/// A support class used as part of setting an exception handler for a specific event handler.
/// For example:
/// <code>disruptorWizard.HandleExceptionsIn(eventHandler).With(exceptionHandler);</code>
/// </summary>
/// <typeparam name="T">the type of event being handled.</typeparam>
public class ValueExceptionHandlerSetting<T> where T : struct
{
    private readonly IValueEventHandler<T> _eventHandler;
    private readonly ConsumerRepository _consumerRepository;

    internal ValueExceptionHandlerSetting(IValueEventHandler<T> eventHandler, ConsumerRepository consumerRepository)
    {
        _eventHandler = eventHandler;
        _consumerRepository = consumerRepository;
    }

    /// <summary>
    /// Specify the <see cref="IValueExceptionHandler{T}"/> to use with the event handler.
    /// </summary>
    /// <param name="exceptionHandler">exceptionHandler the exception handler to use.</param>
    public void With(IValueExceptionHandler<T> exceptionHandler)
    {
        ((IValueEventProcessor<T>)_consumerRepository.GetEventProcessorFor(_eventHandler)).SetExceptionHandler(exceptionHandler);
    }
}
