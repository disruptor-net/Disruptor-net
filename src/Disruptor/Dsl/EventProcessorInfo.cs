namespace Disruptor.Dsl
{
    /// <summary>
    /// Wrapper class to tie together a particular event processing stage
    /// 
    /// Tracks the event processor instance, the event handler instance, and sequence barrier which the stage is attached to.
    /// </summary>
    /// <typeparam name="T">the type of the configured <see cref="IEventHandler{T}"/></typeparam>
    internal class EventProcessorInfo : IConsumerInfo
    {
        public EventProcessorInfo(IEventProcessor eventProcessor, object eventHandler, ISequenceBarrier barrier)
        {
            EventProcessor = eventProcessor;
            Handler = eventHandler;
            Barrier = barrier;
            IsEndOfChain = true;
        }

        public IEventProcessor EventProcessor { get; }

        public ISequence[] Sequences => new[] { EventProcessor.Sequence };

        public object Handler { get; }

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
