using System;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Factory that creates optimized instances of <see cref="IEventProcessor"/>.
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
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IEventHandler<T>), nameof(IEventHandler<T>.OnBatchStart))
            ? new EventProcessorHelpers.DefaultOnBatchStartEvaluator()
            : new EventProcessorHelpers.NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
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
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
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
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(AsyncBatchEventProcessor<,,,,>).MakeGenericType(typeof(T), dataProviderProxy.GetType(), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IAsyncEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IPublishedSequenceReader CreatePublishedSequenceReader(ISequencer sequencer, DependentSequenceGroup dependentSequences)
    {
        if (sequencer is SingleProducerSequencer)
            // The SingleProducerSequencer increments the cursor sequence on publication so the cursor sequence
            // is always published.
            return new EventProcessorHelpers.NoopPublishedSequenceReader();

        if (!dependentSequences.DependsOnCursor)
            // When the sequence barrier does not directly depend on the ring buffer cursor, the dependent sequence
            // is always published (the value is derived from other event processors which cannot process unpublished
            // sequences).
            return new EventProcessorHelpers.NoopPublishedSequenceReader();

        if (sequencer is MultiProducerSequencer multiProducerSequencer)
            // De-virtualize MultiProducerSequencer
            return new EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader(multiProducerSequencer);

        // Fallback for unknown sequencers
        return new EventProcessorHelpers.UnknownSequencerPublishedSequenceReader(sequencer);
    }

    private static IPublishedSequenceReader CreatePublishedSequenceReader(IpcSequenceBarrier sequenceBarrier)
    {
        if (!sequenceBarrier.DependentSequences.DependsOnCursor)
            // When the sequence barrier does not directly depend on the ring buffer cursor, the dependent sequence
            // is always published (the value is derived from other event processors which cannot process unpublished
            // sequences).
            return new EventProcessorHelpers.NoopPublishedSequenceReader();

        return new EventProcessorHelpers.IpcSequencerPublishedSequenceReader(sequenceBarrier.Sequencer);
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
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
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IValueEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProviderProxy, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    /// <summary>
    /// Create a new <see cref="IIpcEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    internal static IIpcEventProcessor<T> Create<T>(IpcRingBuffer<T> dataProvider, SequencePointer sequence, IpcSequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(IpcEventProcessor<,,,,>).MakeGenericType(typeof(T), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IIpcEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequence, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IValueEventHandler<T>), nameof(IValueEventHandler<T>.OnBatchStart))
            ? new EventProcessorHelpers.DefaultOnBatchStartEvaluator()
            : new EventProcessorHelpers.NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
    }

    internal static bool HasNonDefaultImplementation(Type implementationType, Type interfaceType, string methodName)
    {
        var interfaceMap = implementationType.GetInterfaceMap(interfaceType);
        var methodIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceType.GetMethod(methodName));
        var targetMethod = interfaceMap.TargetMethods[methodIndex];
        return targetMethod.DeclaringType != interfaceType;
    }
}
