using System;
using Disruptor.Util;

namespace Disruptor.Processing
{
    /// <summary>
    /// Factory that creates optimized instance of <see cref="IBatchEventProcessor{T}"/>.
    /// </summary>
    public static class BatchEventProcessorFactory
    {
        /// <summary>
        /// Create a new <see cref="IBatchEventProcessor{T}"/> with dedicated generic arguments.
        /// </summary>
        /// <typeparam name="T">the type of event used.</typeparam>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        /// <returns></returns>
        public static IBatchEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler)
            where T : class
        {
            return Create(dataProvider, sequenceBarrier, eventHandler, typeof(BatchEventProcessor<,,,,>));
        }

        internal static IBatchEventProcessor<T> Create<T>(IDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IEventHandler<T> eventHandler, Type processorType)
            where T : class
        {
            var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
            var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
            var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
            var batchStartAwareProxy = CreateBatchStartAwareProxy(eventHandler);

            var batchEventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), batchStartAwareProxy.GetType());
            return (IBatchEventProcessor<T>)Activator.CreateInstance(batchEventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy);
        }

        /// <summary>
        /// Create a new <see cref="IBatchEventProcessor{T}"/> with dedicated generic arguments.
        /// </summary>
        /// <typeparam name="T">the type of event used.</typeparam>
        /// <param name="dataProvider">dataProvider to which events are published</param>
        /// <param name="sequenceBarrier">SequenceBarrier on which it is waiting.</param>
        /// <param name="eventHandler">eventHandler is the delegate to which events are dispatched.</param>
        /// <returns></returns>
        public static IValueBatchEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler)
            where T : struct
        {
            return Create(dataProvider, sequenceBarrier, eventHandler, typeof(ValueBatchEventProcessor<,,,,>));
        }

        internal static IValueBatchEventProcessor<T> Create<T>(IValueDataProvider<T> dataProvider, ISequenceBarrier sequenceBarrier, IValueEventHandler<T> eventHandler, Type processorType)
            where T : struct
        {
            var dataProviderProxy = StructProxy.CreateProxyInstance(dataProvider);
            var sequenceBarrierProxy = StructProxy.CreateProxyInstance(sequenceBarrier);
            var eventHandlerProxy = StructProxy.CreateProxyInstance(eventHandler);
            var batchStartAwareProxy = CreateBatchStartAwareProxy(eventHandler);

            var batchEventProcessorType = processorType.MakeGenericType(typeof(T), dataProviderProxy.GetType(), sequenceBarrierProxy.GetType(), eventHandlerProxy.GetType(), batchStartAwareProxy.GetType());
            return (IValueBatchEventProcessor<T>)Activator.CreateInstance(batchEventProcessorType, dataProviderProxy, sequenceBarrierProxy, eventHandlerProxy, batchStartAwareProxy);
        }

        private static IBatchStartAware CreateBatchStartAwareProxy(object eventHandler)
        {
            if (!(eventHandler is IBatchStartAware batchStartAware))
                return new NoopBatchStartAware();

            var proxy = StructProxy.CreateProxyInstance(batchStartAware);
            var proxyGenerationFailed = ReferenceEquals(proxy, batchStartAware);

            return proxyGenerationFailed ? new DefaultBatchStartAware(batchStartAware) : proxy;
        }

        private struct NoopBatchStartAware : IBatchStartAware
        {
            public void OnBatchStart(long batchSize)
            {
            }
        }

        private struct DefaultBatchStartAware : IBatchStartAware
        {
            private readonly IBatchStartAware _target;

            public DefaultBatchStartAware(IBatchStartAware target)
            {
                _target = target;
            }

            public void OnBatchStart(long batchSize)
            {
                if (batchSize != 0)
                    _target.OnBatchStart(batchSize);
            }
        }
    }
}
