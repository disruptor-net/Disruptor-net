using System;
using System.Collections;
using System.Collections.Generic;

namespace Disruptor.Dsl
{
    internal class ConsumerInfoRepository<T> : IEnumerable<IConsumerInfo> where T : class
    {
        private readonly Dictionary<IEventHandler<T>, EventProcessorInfo<T>> _eventProcessorInfoByEventHandler = new Dictionary<IEventHandler<T>, EventProcessorInfo<T>>();
        private readonly Dictionary<Sequence, IConsumerInfo> _eventProcessorInfoBySequence = new Dictionary<Sequence, IConsumerInfo>();
        private readonly List<IConsumerInfo> _consumerInfos = new List<IConsumerInfo>();

        public void Add(IEventProcessor eventProcessor, IEventHandler<T> eventHandler, ISequenceBarrier sequenceBarrier)
        {
            var consumerInfo = new EventProcessorInfo<T>(eventProcessor, eventHandler, sequenceBarrier);
            _eventProcessorInfoByEventHandler.Add(eventHandler, consumerInfo);
            _eventProcessorInfoBySequence.Add(eventProcessor.Sequence, consumerInfo);
            _consumerInfos.Add(consumerInfo);
        }

        public void Add(IEventProcessor processor)
        {
            var consumerInfo = new EventProcessorInfo<T>(processor, null, null);
            _eventProcessorInfoBySequence.Add(processor.Sequence, consumerInfo);
            _consumerInfos.Add(consumerInfo);
        }
        
        public void Add(WorkerPool<T> workerPool, ISequenceBarrier sequenceBarrier)
        {
            var workerPoolInfo = new WorkerPoolInfo<T>(workerPool, sequenceBarrier);
            _consumerInfos.Add(workerPoolInfo);
            foreach (var sequence in workerPool.WorkerSequences)
            {
                _eventProcessorInfoBySequence.Add(sequence, workerPoolInfo);
            }
        }

        public Sequence[] GetLastEventProcessorsInChain(bool includeStopped)
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
            if(!_eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out eventprocessorInfo) || eventprocessorInfo == null)
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
    }
}
