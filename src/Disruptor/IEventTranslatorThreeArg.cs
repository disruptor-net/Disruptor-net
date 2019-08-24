using System;

namespace Disruptor
{
    /// <summary>
    /// Implementations translate another data representations into events claimed from the <see cref="RingBuffer{T}"/>
    /// </summary>
    /// <typeparam name="T">event implementation storing the data for sharing during exchange or parallel coordination of an event.</typeparam>
    /// <typeparam name="A"></typeparam>
    /// <typeparam name="B"></typeparam>
    /// <typeparam name="C"></typeparam>
    [Obsolete(Constants.ObsoletePublicationApiMessage)]
    public interface IEventTranslatorThreeArg<T, A, B, C>
    {
        /// <summary>
        /// Translate a data representation into fields set in given event
        /// </summary>
        /// <param name="event">into which the data should be translated.</param>
        /// <param name="sequence">that is assigned to event.</param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        void TranslateTo(T @event, long sequence, A arg0, B arg1, C arg2);
    }
}
