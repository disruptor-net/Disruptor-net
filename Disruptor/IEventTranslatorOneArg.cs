namespace Disruptor
{
    public interface IEventTranslatorOneArg<T, A>
    {
        void TranslateTo(T @event, long sequence, A arg0);
    }
}