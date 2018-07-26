using System;

namespace Disruptor.Tests.Support
{
    public class ActionEventTranslator<T> : IEventTranslator<T>, IValueEventTranslator<T>
    {
        private readonly Action<T> _translateToAction;

        public ActionEventTranslator(Action<T> translateToAction)
        {
            _translateToAction = translateToAction;
        }

        public void TranslateTo(T eventData, long sequence)
        {
            _translateToAction.Invoke(eventData);
        }

        public void TranslateTo(ref T eventData, long sequence)
        {
            _translateToAction.Invoke(eventData);
        }
    }
}
