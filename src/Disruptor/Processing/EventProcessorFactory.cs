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
    public static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(EventProcessor<,,,,,>));
    }

    internal static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler, Type processorType)
        where T : class
    {
    var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequencerOptions = sequenceBarrier.GetSequencerOptions();
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequencerOptions.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IEventHandler<T>), nameof(IEventHandler<T>.OnBatchStart))
            ? new DefaultOnBatchStartEvaluator()
            : new NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new DefaultBatchSizeLimiter(maxBatchSize)
            : new NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(BatchEventProcessor<,,,,>));
    }

    internal static IEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler, Type processorType)
        where T : class
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequencerOptions = sequenceBarrier.GetSequencerOptions();
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequencerOptions.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new DefaultBatchSizeLimiter(maxBatchSize)
            : new NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    public static IAsyncEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, AsyncSequenceBarrier sequenceBarrier, IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequencerOptions = sequenceBarrier.GetSequencerOptions();
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(AsyncBatchEventProcessor<,,,,>).MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequencerOptions.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IAsyncEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new DefaultBatchSizeLimiter(maxBatchSize)
            : new NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    public static IValueEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return Create(dataProvider, sequenceBarrier, eventHandler, typeof(ValueEventProcessor<,,,,,>));
    }

    internal static IValueEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler, Type processorType)
        where T : struct
    {
        var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
        var sequencerOptions = sequenceBarrier.GetSequencerOptions();
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequencerOptions.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IValueEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IValueEventHandler<T>), nameof(IValueEventHandler<T>.OnBatchStart))
            ? new DefaultOnBatchStartEvaluator()
            : new NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new DefaultBatchSizeLimiter(maxBatchSize)
            : new NoopBatchSizeLimiter();
    }

    internal static bool HasNonDefaultImplementation(Type implementationType, Type interfaceType, string methodName)
    {
        var interfaceMap = implementationType.GetInterfaceMap(interfaceType);
        var methodIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceType.GetMethod(methodName));
        var targetMethod = interfaceMap.TargetMethods[methodIndex];
        return targetMethod.DeclaringType != interfaceType;
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

    internal struct DefaultBatchSizeLimiter : IBatchSizeLimiter
    {
        private int _maxBatchOffset;

        public DefaultBatchSizeLimiter(int maxBatchOffset)
        {
            _maxBatchOffset = maxBatchOffset - 1;
        }

        public long ApplyMaxBatchSize(long availableSequence, long nextSequence)
        {
            return Math.Min(availableSequence, nextSequence + _maxBatchOffset);
        }
    }

    internal struct NoopBatchSizeLimiter : IBatchSizeLimiter
    {
        public long ApplyMaxBatchSize(long availableSequence, long nextSequence)
        {
            return availableSequence;
        }
    }
}
