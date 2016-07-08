namespace Disruptor.Dsl
{
    internal class EventProcessorInfo<T> : IConsumerInfo
    {
        public EventProcessorInfo(IEventProcessor eventProcessor, IEventHandler<T> eventHandler, ISequenceBarrier barrier)
        {
            EventProcessor = eventProcessor;
            Handler = eventHandler;
            Barrier = barrier;
            IsEndOfChain = true;
        }

        public IEventProcessor EventProcessor { get; }

        public Sequence[] Sequences => new[] { EventProcessor.Sequence };

        public IEventHandler<T> Handler { get; }

        public ISequenceBarrier Barrier { get; }

        public bool IsEndOfChain { get; private set; }

        public void Start(IExecutor executor)
        {
            executor.Execute(EventProcessor.Run);
        }

        public void Halt()
        {
            EventProcessor.Halt();
        }

        public void MarkAsUsedInBarrier()
        {
            IsEndOfChain = false;
        }

        public bool IsRunning => EventProcessor.IsRunning;
    }
}
