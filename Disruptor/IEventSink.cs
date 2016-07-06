namespace Disruptor
{
    /// <summary>
    ///     An entity into which events can be published
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IEventSink<T> where T : class
    {
        /// <summary>
        ///     Publishes an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        ///     event from the ring buffer and publishing the claimed sequence after translation.
        /// </summary>
        /// <param name="translator"></param>
        void PublishEvent(IEventTranslator<T> translator);

        /// <summary>
        /// Attempts to publish an event to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity
        /// was not available.
        /// </summary>
        /// <param name="translator"></param>
        /// <returns></returns>
        bool TryPublishEvent(IEventTranslator<T> translator);

        /// <summary>
        /// Allows one user supplied argument.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        void PublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0);

        /// <summary>
        /// Allows one user supplied argument.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        /// <returns></returns>
        bool TryPublishEvent<A>(IEventTranslatorOneArg<T, A> translator, A arg0);

        /// <summary>
        /// Allows two user supplied arguments.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        void PublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1);

        /// <summary>
        /// Allows two user supplied arguments.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <returns></returns>
        bool TryPublishEvent<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A arg0, B arg1);

        /// <summary>
        /// Allows three user supplied arguments
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        void PublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2);

        /// <summary>
        /// Allows three user supplied arguments
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <returns></returns>
        bool TryPublishEvent<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A arg0, B arg1, C arg2);

        /// <summary>
        /// Allows a variable number of user supplied arguments
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="args"></param>
        void PublishEvent(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        bool TryPublishEvent(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators"></param>
        void PublishEvents(IEventTranslator<T>[] translators);

        /// <summary>
        /// Publishes multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.
        /// <para/>
        /// With this call the data that is to be inserted into the ring buffer will be a field (either explicitly or captured anonymously),
        /// therefore this call will require an instance of the translator for each value that is to be inserted into the ring buffer.
        /// </summary>
        /// <param name="translators"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        void PublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize);

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators"></param>
        /// <returns></returns>
        bool TryPublishEvents(IEventTranslator<T>[] translators);

        /// <summary>
        /// Attempts to publish multiple events to the ring buffer.  It handles claiming the next sequence, getting the current(uninitialised)
        /// event from the ring buffer and publishing the claimed sequence after translation.Will return false if specified capacity was not available.
        /// </summary>
        /// <param name="translators"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <returns></returns>
        bool TryPublishEvents(IEventTranslator<T>[] translators, int batchStartsAt, int batchSize);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        void PublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, A[] arg0);

        /// <summary>
        /// Allows one user supplied argument per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <typeparam name="A"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A>(IEventTranslatorOneArg<T, A> translator, int batchStartsAt, int batchSize, A[] arg0);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        void PublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows two user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A, B>(IEventTranslatorTwoArg<T, A, B> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        void PublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows three user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="arg0"></param>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        /// <typeparam name="A"></typeparam>
        /// <typeparam name="B"></typeparam>
        /// <typeparam name="C"></typeparam>
        /// <returns></returns>
        bool TryPublishEvents<A, B, C>(IEventTranslatorThreeArg<T, A, B, C> translator, int batchStartsAt, int batchSize, A[] arg0, B[] arg1, C[] arg2);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="args"></param>
        void PublishEvents(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="args"></param>
        void PublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        bool TryPublishEvents(IEventTranslatorVararg<T> translator, params object[] args);

        /// <summary>
        /// Allows a variable number of user supplied arguments per event.
        /// </summary>
        /// <param name="translator"></param>
        /// <param name="batchStartsAt"></param>
        /// <param name="batchSize"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        bool TryPublishEvents(IEventTranslatorVararg<T> translator, int batchStartsAt, int batchSize, params object[][] args);
    }
}
