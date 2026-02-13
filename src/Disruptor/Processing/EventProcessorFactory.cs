using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Disruptor.Util;

namespace Disruptor.Processing;

/// <summary>
/// Factory that creates optimized instances of <see cref="IEventProcessor"/>.
/// </summary>
public static class EventProcessorFactory
{
    /// <summary>
    /// Indicates whether dynamic code is enabled.
    /// </summary>
    internal static bool IsDynamicCodeEnabled { get; } = Environment.GetEnvironmentVariable("DISRUPTOR_DYNAMIC_CODE_DISABLED")?.ToLowerInvariant() switch
    {
        "1"    => false,
        "true" => false,
        _      => true,
    };

    private static bool IsDynamicCodeSupportedAndEnabled
        => RuntimeFeature.IsDynamicCodeSupported && IsDynamicCodeEnabled;

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">sequence barrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode", Justification = DynamicCodeNotReachableWithAot)]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = DynamicCodeNotReachableWithAot)]
    public static IEventProcessor<T> Create<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        return IsDynamicCodeSupportedAndEnabled
            ? CreateDynamic(dataProvider, sequenceBarrier, eventHandler)
            : CreateStatic(dataProvider, sequenceBarrier, eventHandler);
    }

    [RequiresDynamicCode(TypeOrMethodNotReachableForAot)]
    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IEventProcessor<T> CreateDynamic<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(EventProcessor<,,,,>).MakeGenericType(typeof(T), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IEventProcessor<T> CreateStatic<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
        where T : class
    {
        var bse = new EventProcessorHelpers.DefaultOnBatchStartEvaluator();

        return CreateBatchSizeLimiter(eventHandler) switch
        {
            EventProcessorHelpers.DefaultBatchSizeLimiter bsl => CreateWithBatchSizeLimiter(bsl),
            EventProcessorHelpers.NoopBatchSizeLimiter bsl    => CreateWithBatchSizeLimiter(bsl),
            _                                                 => throw NotSupportedType("BatchSizeLimiter"),
        };

        IEventProcessor<T> CreateWithBatchSizeLimiter<TBatchSizeLimiter>(TBatchSizeLimiter bsl)
            where TBatchSizeLimiter : struct, IBatchSizeLimiter
        {
            return CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences) switch
            {
                EventProcessorHelpers.NoopPublishedSequenceReader psr                   => new EventProcessor<T, EventProcessorHelpers.NoopPublishedSequenceReader, IEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader psr => new EventProcessor<T, EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader, IEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                EventProcessorHelpers.UnknownSequencerPublishedSequenceReader psr       => new EventProcessor<T, EventProcessorHelpers.UnknownSequencerPublishedSequenceReader, IEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                _                                                                       => throw NotSupportedType("PublishedSequenceReader"),
            };
        }
    }

    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        // If you add new IOnBatchStartEvaluator implementations, please update CreateStatic methods accordingly.

        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IEventHandler<T>), nameof(IEventHandler<T>.OnBatchStart))
            ? new EventProcessorHelpers.DefaultOnBatchStartEvaluator()
            : new EventProcessorHelpers.NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IEventHandler<T> eventHandler)
        where T : class
    {
        // If you add new IBatchSizeLimiter implementations, please update CreateStatic methods accordingly.

        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">sequence barrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode", Justification = DynamicCodeNotReachableWithAot)]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = DynamicCodeNotReachableWithAot)]
    public static IEventProcessor<T> Create<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return IsDynamicCodeSupportedAndEnabled
            ? CreateDynamic(dataProvider, sequenceBarrier, eventHandler)
            : CreateStatic(dataProvider, sequenceBarrier, eventHandler);
    }

    [RequiresDynamicCode(TypeOrMethodNotReachableForAot)]
    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IEventProcessor<T> CreateDynamic<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
        where T : class
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);

        var eventProcessorType = typeof(BatchEventProcessor<,,,>).MakeGenericType(typeof(T), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IEventProcessor<T> CreateStatic<T>(RingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IBatchEventHandler<T> eventHandler)
        where T : class
    {
        return CreateBatchSizeLimiter(eventHandler) switch
        {
            EventProcessorHelpers.DefaultBatchSizeLimiter bsl => CreateWithBatchSizeLimiter(bsl),
            EventProcessorHelpers.NoopBatchSizeLimiter bsl    => CreateWithBatchSizeLimiter(bsl),
            _                                                 => throw NotSupportedType("BatchSizeLimiter"),
        };

        IEventProcessor<T> CreateWithBatchSizeLimiter<TBatchSizeLimiter>(TBatchSizeLimiter bsl)
            where TBatchSizeLimiter : IBatchSizeLimiter
        {
            return CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences) switch
            {
                EventProcessorHelpers.NoopPublishedSequenceReader psr                   => new BatchEventProcessor<T, EventProcessorHelpers.NoopPublishedSequenceReader, IBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader psr => new BatchEventProcessor<T, EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader, IBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                EventProcessorHelpers.UnknownSequencerPublishedSequenceReader psr       => new BatchEventProcessor<T, EventProcessorHelpers.UnknownSequencerPublishedSequenceReader, IBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                _                                                                       => throw NotSupportedType("PublishedSequenceReader"),
            };
        }
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IBatchEventHandler<T> eventHandler)
        where T : class
    {
        // If you add new IBatchSizeLimiter implementations, please update CreateStatic methods accordingly.

        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">sequence barrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    /// <returns></returns>
    [UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode", Justification = DynamicCodeNotReachableWithAot)]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = DynamicCodeNotReachableWithAot)]
    public static IAsyncEventProcessor<T> Create<T>(RingBuffer<T> dataProvider, AsyncSequenceBarrier sequenceBarrier, IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        return IsDynamicCodeSupportedAndEnabled
            ? CreateDynamic(dataProvider, sequenceBarrier, eventHandler)
            : CreateStatic(dataProvider, sequenceBarrier, eventHandler);
    }

    [RequiresDynamicCode(TypeOrMethodNotReachableForAot)]
    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IAsyncEventProcessor<T> CreateDynamic<T>(RingBuffer<T> dataProvider, AsyncSequenceBarrier sequenceBarrier, IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(AsyncBatchEventProcessor<,,,>).MakeGenericType(typeof(T), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), batchSizeLimiter.GetType());
        return (IAsyncEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, batchSizeLimiter)!;
    }

    private static IAsyncEventProcessor<T> CreateStatic<T>(RingBuffer<T> dataProvider, AsyncSequenceBarrier sequenceBarrier, IAsyncBatchEventHandler<T> eventHandler)
        where T : class
    {
        return CreateBatchSizeLimiter(eventHandler) switch
        {
            EventProcessorHelpers.DefaultBatchSizeLimiter bsl => CreateWithBatchSizeLimiter(bsl),
            EventProcessorHelpers.NoopBatchSizeLimiter bsl    => CreateWithBatchSizeLimiter(bsl),
            _                                                 => throw NotSupportedType("BatchSizeLimiter"),
        };

        IAsyncEventProcessor<T> CreateWithBatchSizeLimiter<TBatchSizeLimiter>(TBatchSizeLimiter bsl)
            where TBatchSizeLimiter : IBatchSizeLimiter
        {
            return CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences) switch
            {
                EventProcessorHelpers.NoopPublishedSequenceReader psr                   => new AsyncBatchEventProcessor<T, EventProcessorHelpers.NoopPublishedSequenceReader, IAsyncBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader psr => new AsyncBatchEventProcessor<T, EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader, IAsyncBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                EventProcessorHelpers.UnknownSequencerPublishedSequenceReader psr       => new AsyncBatchEventProcessor<T, EventProcessorHelpers.UnknownSequencerPublishedSequenceReader, IAsyncBatchEventHandler<T>, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bsl),
                _                                                                       => throw NotSupportedType("PublishedSequenceReader"),
            };
        }
    }

    private static IPublishedSequenceReader CreatePublishedSequenceReader(ISequencer sequencer, DependentSequenceGroup dependentSequences)
    {
        // If you add new IPublishedSequenceReader implementations, please update CreateStatic methods accordingly.

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
        // If you add new IPublishedSequenceReader implementations, please update CreateStatic methods accordingly.

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
        // If you add new IBatchSizeLimiter implementations, please update CreateStatic methods accordingly.

        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">sequence barrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    public static IValueEventProcessor<T> Create<T>(ValueRingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
    {
        return Create(new EventProcessorHelpers.ValueRingBufferDataProvider<T>(dataProvider), sequenceBarrier, eventHandler);
    }

    /// <summary>
    /// Create a new <see cref="IEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    /// <param name="dataProvider">dataProvider to which events are published</param>
    /// <param name="sequenceBarrier">sequence barrier on which it is waiting.</param>
    /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
    public static IValueEventProcessor<T> Create<T>(UnmanagedRingBuffer<T> dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        return Create(new EventProcessorHelpers.UnmanagedRingBufferDataProvider<T>(dataProvider), sequenceBarrier, eventHandler);
    }

    [UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode", Justification = DynamicCodeNotReachableWithAot)]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = DynamicCodeNotReachableWithAot)]
    private static IValueEventProcessor<T> Create<T, TDataProvider>(TDataProvider dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
        where TDataProvider : struct, IValueDataProvider<T>
    {
        return IsDynamicCodeSupportedAndEnabled
            ? CreateDynamic(dataProvider, sequenceBarrier, eventHandler)
            : CreateStatic(dataProvider, sequenceBarrier, eventHandler);
    }

    [RequiresDynamicCode(TypeOrMethodNotReachableForAot)]
    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IValueEventProcessor<T> CreateDynamic<T, TDataProvider>(TDataProvider dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
        where TDataProvider : struct, IValueDataProvider<T>
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(ValueEventProcessor<,,,,,>).MakeGenericType(typeof(T), dataProvider.GetType(), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IValueEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IValueEventProcessor<T> CreateStatic<T, TDataProvider>(TDataProvider dataProvider, SequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : struct
        where TDataProvider : struct, IValueDataProvider<T>
    {
        var bse = new EventProcessorHelpers.DefaultOnBatchStartEvaluator();

        return CreateBatchSizeLimiter(eventHandler) switch
        {
            EventProcessorHelpers.DefaultBatchSizeLimiter bsl => CreateWithBatchSizeLimiter(bsl),
            EventProcessorHelpers.NoopBatchSizeLimiter bsl    => CreateWithBatchSizeLimiter(bsl),
            _                                                 => throw NotSupportedType("BatchSizeLimiter"),
        };

        IValueEventProcessor<T> CreateWithBatchSizeLimiter<TBatchSizeLimiter>(TBatchSizeLimiter bsl)
            where TBatchSizeLimiter : struct, IBatchSizeLimiter
        {
            return CreatePublishedSequenceReader(sequenceBarrier.Sequencer, sequenceBarrier.DependentSequences) switch
            {
                EventProcessorHelpers.NoopPublishedSequenceReader psr                   => new ValueEventProcessor<T, TDataProvider, EventProcessorHelpers.NoopPublishedSequenceReader, IValueEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader psr => new ValueEventProcessor<T, TDataProvider, EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader, IValueEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                EventProcessorHelpers.UnknownSequencerPublishedSequenceReader psr       => new ValueEventProcessor<T, TDataProvider, EventProcessorHelpers.UnknownSequencerPublishedSequenceReader, IValueEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequenceBarrier, psr, eventHandler, bse, bsl),
                _                                                                       => throw NotSupportedType("PublishedSequenceReader"),
            };
        }
    }

    /// <summary>
    /// Create a new <see cref="IIpcEventProcessor{T}"/> with dedicated generic arguments.
    /// </summary>
    /// <typeparam name="T">the type of event used.</typeparam>
    [UnconditionalSuppressMessage("Aot", "IL3050:RequiresDynamicCode", Justification = DynamicCodeNotReachableWithAot)]
    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = DynamicCodeNotReachableWithAot)]
    internal static IIpcEventProcessor<T> Create<T>(IpcRingBuffer<T> dataProvider, SequencePointer sequence, IpcSequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        return IsDynamicCodeSupportedAndEnabled
            ? CreateDynamic(dataProvider, sequence, sequenceBarrier, eventHandler)
            : CreateStatic(dataProvider, sequence, sequenceBarrier, eventHandler);
    }

    [RequiresDynamicCode(TypeOrMethodNotReachableForAot)]
    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IIpcEventProcessor<T> CreateDynamic<T>(IpcRingBuffer<T> dataProvider, SequencePointer sequence, IpcSequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        var publishedSequenceReader = CreatePublishedSequenceReader(sequenceBarrier);
        var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
        var onBatchStartInvoker = CreateOnBatchStartEvaluator(eventHandler);
        var batchSizeLimiter = CreateBatchSizeLimiter(eventHandler);

        var eventProcessorType = typeof(IpcEventProcessor<,,,,>).MakeGenericType(typeof(T), publishedSequenceReader.GetType(), eventHandlerProxy.GetType(), onBatchStartInvoker.GetType(), batchSizeLimiter.GetType());
        return (IIpcEventProcessor<T>)Activator.CreateInstance(eventProcessorType, dataProvider, sequence, sequenceBarrier, publishedSequenceReader, eventHandlerProxy, onBatchStartInvoker, batchSizeLimiter)!;
    }

    private static IIpcEventProcessor<T> CreateStatic<T>(IpcRingBuffer<T> dataProvider, SequencePointer sequence, IpcSequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
        where T : unmanaged
    {
        var bse = new EventProcessorHelpers.DefaultOnBatchStartEvaluator();

        return CreateBatchSizeLimiter(eventHandler) switch
        {
            EventProcessorHelpers.DefaultBatchSizeLimiter bsl => CreateWithBatchSizeLimiter(bsl),
            EventProcessorHelpers.NoopBatchSizeLimiter bsl    => CreateWithBatchSizeLimiter(bsl),
            _                                                 => throw NotSupportedType("OnBatchStartEvaluator"),
        };

        IIpcEventProcessor<T> CreateWithBatchSizeLimiter<TBatchSizeLimiter>(TBatchSizeLimiter bsl)
            where TBatchSizeLimiter : struct, IBatchSizeLimiter
        {
            return CreatePublishedSequenceReader(sequenceBarrier) switch
            {
                EventProcessorHelpers.NoopPublishedSequenceReader psr         => new IpcEventProcessor<T, EventProcessorHelpers.NoopPublishedSequenceReader, IValueEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequence, sequenceBarrier, psr, eventHandler, bse, bsl),
                EventProcessorHelpers.IpcSequencerPublishedSequenceReader psr => new IpcEventProcessor<T, EventProcessorHelpers.IpcSequencerPublishedSequenceReader, IValueEventHandler<T>, EventProcessorHelpers.DefaultOnBatchStartEvaluator, TBatchSizeLimiter>(dataProvider, sequence, sequenceBarrier, psr, eventHandler, bse, bsl),
                _                                                             => throw NotSupportedType("OnBatchStartEvaluator"),
            };
        }
    }

    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    private static IOnBatchStartEvaluator CreateOnBatchStartEvaluator<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        // If you add new IOnBatchStartEvaluator implementations, please update CreateStatic methods accordingly.

        return HasNonDefaultImplementation(eventHandler.GetType(), typeof(IValueEventHandler<T>), nameof(IValueEventHandler<T>.OnBatchStart))
            ? new EventProcessorHelpers.DefaultOnBatchStartEvaluator()
            : new EventProcessorHelpers.NoopOnBatchStartEvaluator();
    }

    private static IBatchSizeLimiter CreateBatchSizeLimiter<T>(IValueEventHandler<T> eventHandler)
        where T : struct
    {
        // If you add new IBatchSizeLimiter implementations, please update CreateStatic methods accordingly.

        return eventHandler.MaxBatchSize is { } maxBatchSize
            ? new EventProcessorHelpers.DefaultBatchSizeLimiter(maxBatchSize)
            : new EventProcessorHelpers.NoopBatchSizeLimiter();
    }

    [RequiresUnreferencedCode(TypeOrMethodNotReachableForAot)]
    internal static bool HasNonDefaultImplementation(Type implementationType, Type interfaceType, string methodName)
    {
        var interfaceMap = implementationType.GetInterfaceMap(interfaceType);
        var methodIndex = Array.IndexOf(interfaceMap.InterfaceMethods, interfaceType.GetMethod(methodName));
        var targetMethod = interfaceMap.TargetMethods[methodIndex];
        return targetMethod.DeclaringType != interfaceType;
    }

    private static NotSupportedException NotSupportedType(string typeName)
        => new($"Unsupported {typeName}.");
}
