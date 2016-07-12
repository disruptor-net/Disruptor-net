using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Disruptor.Dsl
{
    internal class ConsumerRepository<T> : IEnumerable<IConsumerInfo> where T : class
    {
        private readonly Dictionary<IEventHandler<T>, EventProcessorInfo<T>> _eventProcessorInfoByEventHandler;
        private readonly Dictionary<ISequence, IConsumerInfo> _eventProcessorInfoBySequence;
        private readonly List<IConsumerInfo> _consumerInfos = new List<IConsumerInfo>();

        public ConsumerRepository()
        {
            _eventProcessorInfoByEventHandler = new Dictionary<IEventHandler<T>, EventProcessorInfo<T>>(new IdentityComparer<IEventHandler<T>>());
            _eventProcessorInfoBySequence = new Dictionary<ISequence, IConsumerInfo>(new IdentityComparer<ISequence>());
        }

        public void Add(IEventProcessor eventProcessor, IEventHandler<T> eventHandler, ISequenceBarrier sequenceBarrier)
        {
            var consumerInfo = new EventProcessorInfo<T>(eventProcessor, eventHandler, sequenceBarrier);
            _eventProcessorInfoByEventHandler[eventHandler] = consumerInfo;
            _eventProcessorInfoBySequence[eventProcessor.Sequence] = consumerInfo;
            _consumerInfos.Add(consumerInfo);
        }

        public void Add(IEventProcessor processor)
        {
            var consumerInfo = new EventProcessorInfo<T>(processor, null, null);
            _eventProcessorInfoBySequence [processor.Sequence] = consumerInfo;
            _consumerInfos.Add(consumerInfo);
        }
        
        public void Add(WorkerPool<T> workerPool, ISequenceBarrier sequenceBarrier)
        {
            var workerPoolInfo = new WorkerPoolInfo<T>(workerPool, sequenceBarrier);
            _consumerInfos.Add(workerPoolInfo);
            foreach (var sequence in workerPool.WorkerSequences)
            {
                _eventProcessorInfoBySequence[sequence] = workerPoolInfo;
            }
        }

        public ISequence[] GetLastSequenceInChain(bool includeStopped)
        {
            var lastSequence = new List<ISequence>();
            foreach (var consumerInfo in _consumerInfos)
            {
                if ((includeStopped || consumerInfo.IsRunning) && consumerInfo.IsEndOfChain)
                {
                    var sequences = consumerInfo.Sequences;
                    lastSequence.AddRange(sequences);
                }
            }

            return lastSequence.ToArray();
        }

        public IEventProcessor GetEventProcessorFor(IEventHandler<T> eventHandler)
        {
            EventProcessorInfo<T> eventprocessorInfo;
            var found = _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out eventprocessorInfo);
            if(!found || eventprocessorInfo == null)
            {
                throw new ArgumentException("The event handler " + eventHandler + " is not processing events.");
            }

            return eventprocessorInfo.EventProcessor;
        }

        public ISequence GetSequenceFor(IEventHandler<T> eventHandler)
        {
            return GetEventProcessorFor(eventHandler).Sequence;
        }

        public void UnMarkEventProcessorsAsEndOfChain(params ISequence[] barrierEventProcessors)
        {
            foreach (var barrierEventProcessor in barrierEventProcessors)
            {
                IConsumerInfo consumerInfo;
                if (_eventProcessorInfoBySequence.TryGetValue(barrierEventProcessor, out consumerInfo))
                {
                    consumerInfo.MarkAsUsedInBarrier();
                }                
            }
        }

        public ISequenceBarrier GetBarrierFor(IEventHandler<T> eventHandler)
        {
            EventProcessorInfo<T> eventProcessorInfo;
            return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out eventProcessorInfo) ? eventProcessorInfo.Barrier : null;
        }

        public IEnumerator<IConsumerInfo> GetEnumerator() => _consumerInfos.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class IdentityComparer<TKey> : IEqualityComparer<TKey>
        {
            public bool Equals(TKey x, TKey y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(TKey obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
