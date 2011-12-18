using System;
using System.Collections.Generic;
using System.Linq;

namespace Disruptor.Dsl
{
    internal class EventProcessorRepository<T>
    {
        private readonly IDictionary<IEventHandler<T>, EventProcessorInfo<T>> _eventProcessorInfoByHandler = new Dictionary<IEventHandler<T>, EventProcessorInfo<T>>();
        private readonly IDictionary<IEventProcessor, EventProcessorInfo<T>> _eventProcessorInfoByEventProcessor = new Dictionary<IEventProcessor, EventProcessorInfo<T>>();

        public void Add(IEventProcessor eventProcessor, IEventHandler<T> eventHandler, ISequenceBarrier sequenceBarrier)
        {
            var eventProcessorInfo = new EventProcessorInfo<T>(eventProcessor, eventHandler, sequenceBarrier);
            _eventProcessorInfoByHandler[eventHandler] = eventProcessorInfo;
            _eventProcessorInfoByEventProcessor[eventProcessor] = eventProcessorInfo;
        }

        public void Add(IEventProcessor processor)
        {
            var eventProcessorInfo = new EventProcessorInfo<T>(processor, null, null);
            _eventProcessorInfoByEventProcessor[processor] = eventProcessorInfo;
        }

        public IEventProcessor[] LastEventProcessorsInChain
        {
            get
            {
                return (from eventProcessorInfo in _eventProcessorInfoByHandler.Values
                        where eventProcessorInfo.IsEndOfChain
                        select eventProcessorInfo.EventProcessor).ToArray();
            }
        }

        public IEventProcessor GetEventProcessorFor(IEventHandler<T> eventHandler)
        {
            var eventProcessorInfo = _eventProcessorInfoByHandler[eventHandler];
            if(eventProcessorInfo == null)
            {
                throw new ArgumentException("The event handler " + eventHandler + " is not processing events.");
            }

            return eventProcessorInfo.EventProcessor;
        }

        public void UnmarkEventProcessorsAsEndOfChain(IEnumerable<IEventProcessor> eventProcessors)
        {
            foreach (var eventProcessor in eventProcessors)
            {
                _eventProcessorInfoByEventProcessor[eventProcessor].MarkAsUsedInBarrier();
            }
        }

        public IEnumerable<EventProcessorInfo<T>> EventProcessors
        {
            get { return _eventProcessorInfoByHandler.Values; }
        }

        public ISequenceBarrier GetBarrierFor(IEventHandler<T> handler)
        {
            var eventProcessorInfo = GetEventProcessorInfo(handler);
            return eventProcessorInfo != null ? eventProcessorInfo.SequenceBarrier : null;
        }

        private EventProcessorInfo<T> GetEventProcessorInfo(IEventHandler<T> handler)
        {
            return _eventProcessorInfoByHandler[handler];
        }

        private EventProcessorInfo<T> GetEventProcessorInfo(IEventProcessor barrierEventProcessor)
        {
            return _eventProcessorInfoByEventProcessor[barrierEventProcessor];
        }
    }
}
