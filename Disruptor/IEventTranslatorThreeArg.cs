namespace Disruptor
{
    public interface IEventTranslatorThreeArg<T, A, B, C>
    {
        /**
         * Translate a data representation into fields set in given event
         *
         * @param event    into which the data should be translated.
         * @param sequence that is assigned to event.
         * @param arg0     The first user specified argument to the translator
         * @param arg1     The second user specified argument to the translator
         * @param arg2     The third user specified argument to the translator
         */
        void TranslateTo(T @event, long sequence, A arg0, B arg1, C arg2);
    }
}