namespace Disruptor
{
    public interface IEventTranslatorTwoArg<T, A, B>
    {
        void TranslateTo(T @event, long sequence, A arg0, B arg1);
    }
}