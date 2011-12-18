using Disruptor.Dsl;

namespace Disruptor
{
    ///<summary>
    ///  A group of <see cref="IEventHandler{T}"/> set up via the <see cref="RingBuffer{T}"/>
    ///</summary>
    ///<typeparam name="T">the type of event used by the <see cref="IEventProcessor"/>s.</typeparam>
    public interface IEventHandlerGroup<out T>
    {
        ///<summary>
        /// Set up <see cref="IEventHandler{T}"/>s to consume events from the ring buffer. These handlers will only process events
        /// after every event processor in this group has processed the event.
        /// 
        /// This method is generally used as part of a chain. For example if the handler <code>A</code> must
        /// process events before handler <code>B</code>:
        /// <pre><code>dw.consumeWith(A).then(B);</code></pre>
        ///</summary>
        ///<param name="eventHandlers">handlers the batch handlers that will consume events.</param>
        ///<returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up a <see cref="ISequenceBarrier"/> over the created <see cref="IEventProcessor"/>s.</returns>
        IEventHandlerGroup<T> Then(params IEventHandler<T>[] eventHandlers);

        ///<summary>
        /// Set up <see cref="IEventHandler{T}"/>s to consume events from the ring buffer. These handlers will only process events
        /// after every event processor in this group has processed the event.
        /// 
        /// <p>This method is generally used as part of a chain. For example if the handler <code>A</code> must
        /// process events before handler <code>B</code>:</p>
        /// 
        /// <pre><code>dw.after(A).consumeWith(B);</code></pre>
        ///</summary>
        ///<param name="eventHandlers">the <see cref="IEventHandler{T}"/> that will consume events.</param>
        ///<returns>a <see cref="EventHandlerGroup{T}"/> that can be used to set up an <see cref="ISequenceBarrier"/> over the created <see cref="IEventProcessor"/>s.</returns>
        IEventHandlerGroup<T> ConsumeWith(IEventHandler<T>[] eventHandlers);
    }
}