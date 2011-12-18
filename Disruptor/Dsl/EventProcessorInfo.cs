namespace Disruptor.Dsl
{
    internal class EventProcessorInfo<T>
    {
        public EventProcessorInfo(IEventProcessor eventProcessor, IEventHandler<T> eventHandler, ISequenceBarrier sequenceBarrier)
        {
            EventProcessor = eventProcessor;
            EventHandler = eventHandler;
            SequenceBarrier = sequenceBarrier;
            IsEndOfChain = true;
        }

        public IEventProcessor EventProcessor { get; private set; }
        public IEventHandler<T> EventHandler { get; private set; }
        public ISequenceBarrier SequenceBarrier { get; private set; }

        public bool IsEndOfChain { get; private set; }

        public void MarkAsUsedInBarrier()
        {
            IsEndOfChain = false;
        }
    }
}
