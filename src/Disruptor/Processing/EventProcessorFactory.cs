using System;
using System.Runtime.CompilerServices;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Factory that creates optimized instance of <see cref="IEventProcessor"/>.
/// </summary>
public static class EventProcessorFactory
{
    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(EventProcessor<,,,,>));
    }

    internal static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler, Type processorType)
        where T : class
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, onBatchStartInvoker)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        var methodInfo = eventHandler.GetType().GetMethod(nameof(IEventHandler<T>.OnBatchStart));

        return methodInfo == null ? new NoopOnBatchStartEvaluator() : new DefaultOnBatchStartEvaluator();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(BatchEventProcessor<,,,>));
    }

    internal static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler, Type processorType)
        where T : class
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy)!;
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IAsyncEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, IAsyncSequenceBarrier sequenceBarrier, IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);

        var eventProcessorType = typeof(AsyncBatchEventProcessor<,,,>).MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType());
        return (IAsyncEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy)!;
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IValueEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(ValueEventProcessor<,,,,>));
    }

    internal static IValueEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler, Type processorType)
        where T : struct
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType());
        return (IValueEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, onBatchStartInvoker)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        var methodInfo = eventHandler.GetType().GetMethod(nameof(IValueEventHandler<T>.OnBatchStart));

        return methodInfo == null ? new NoopOnBatchStartEvaluator() : new DefaultOnBatchStartEvaluator();
    }

    internal struct NoopOnBatchStartEvaluator : IOnBatchStartEvaluator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldInvokeOnBatchStart(long availableSequence, long nextSequence)
        {
            return false;
        }
    }

    internal struct DefaultOnBatchStartEvaluator : IOnBatchStartEvaluator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool ShouldInvokeOnBatchStart(long availableSequence, long nextSequence)
        {
            return availableSequence >= nextSequence;
        }
    }
}