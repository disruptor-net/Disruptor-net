using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Disruptor.Dsl
{
    internal class ConsumerRepository<T> : IEnumerable<IConsumerInfo> where T : class
    {
        private readonly Dictionary<IEventHandler<T>, EventProcessorInfo<T>> _eventProcessorInfoByEventHandler;
        private readonly Dictionary<Sequence, IConsumerInfo> _eventProcessorInfoBySequence;
        private readonly List<IConsumerInfo> _consumerInfos = new List<IConsumerInfo>();

        public ConsumerRepository()
        {
            _eventProcessorInfoByEventHandler = new Dictionary<IEventHandler<T>, EventProcessorInfo<T>>(new IdentityComparer<IEventHandler<T>>());
            _eventProcessorInfoBySequence = new Dictionary<Sequence, IConsumerInfo>(new IdentityComparer<Sequence>());
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

        public Sequence[] GetLastSequenceInChain(bool includeStopped)
        {
            var lastSequence = new List<Sequence>();
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

        public Sequence GetSequenceFor(IEventHandler<T> eventHandler)
        {
            return GetEventProcessorFor(eventHandler).Sequence;
        }

        public void UnMarkEventProcessorsAsEndOfChain(params Sequence[] barrierEventProcessors)
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
            return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out eventProcessorInfo) ? eventProcessorInfo.SequenceBarrier : null;
        }

        public IEnumerator<IConsumerInfo> GetEnumerator() => _consumerInfos.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class IdentityComparer<T> : IEqualityComparer<T>
        {
            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
