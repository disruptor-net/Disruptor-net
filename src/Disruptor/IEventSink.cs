using System;

namespace Disruptor
{
    /// <summary>
    ///     An entity into which events can be published
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Obsolete(Constants.ObsoletePublicationApiMessage)]
    public interface IEventSink<T> where T : class
    {
        /// <summary>
        /// Publishes an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        void PublishEvent(IEventTranslator<T> translator);

        /// <summary>
        /// Attempts to publish an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity
        /// was not available.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvent(IEventTranslator<T> translator);

        /// <summary>
        /// Allows one user supplied argument.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        void PublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0);

        /// <summary>
        /// Allows one user supplied argument.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0);

        /// <summary>
        /// Allows two user supplied arguments.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        void PublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1);

        /// <summary>
        /// Allows two user supplied arguments.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1);

        /// <summary>
        /// Allows three user supplied arguments
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        void PublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2);

        /// <summary>
        /// Allows three user supplied arguments
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2);

        /// <summary>
        /// Allows a variable number of user supplied arguments
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        void PublishEvent(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvent(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        void PublishEvents(IEventTranslator<T>[] translators);

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        void PublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize);

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents(IEventTranslator<T>[] translators);

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators">The user specified translation for each event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <typeparam name="A">Class of the user supplied argument</typeparam>
        /// <typeparam name="B">Class of the user supplied argument</typeparam>
        /// <typeparam name="C">Class of the user supplied argument</typeparam>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="arg0">A user supplied argument.</param>
        /// <param name="arg1">A user supplied argument.</param>
        /// <param name="arg2">A user supplied argument.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        void PublishEvents(IEventTranslatorVararg<T> translator, params object[][] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        void PublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents(IEventTranslatorVararg<T> translator, params object[][] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator">The user specified translation for the event</param>
        /// <param name="batchStartsAt">The first element of the array which is within the batch.</param>
        /// <param name="batchSize">The actual size of the batch.</param>
        /// <param name="args">User supplied arguments, one Object[] per event.</param>
        /// <returns>true if the value was published, false if there was insufficient capacity</returns>
        bool TryPublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args);
    }
}
