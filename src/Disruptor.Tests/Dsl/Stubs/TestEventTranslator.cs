using System;

namespace Disruptor.Tests.Dsl.Stubs
{
    public class TestEventTranslator<T> : IEventTranslator<T>
    {
        private readonly Action<T, long> _translateToAction;

        public TestEventTranslator(Action translateToAction)
            : this((eventData, sequence) => translateToAction.Invoke())
        {
        }

        public TestEventTranslator(Action<T> translateToAction)
            : this((eventData, sequence) => translateToAction.Invoke(eventData))
        {
        }

        public TestEventTranslator(Action<T, long> translateToAction)
        {
            _translateToAction = translateToAction;
        }

        public void TranslateTo(T eventData, long sequence)
        {
            _translateToAction.Invoke(eventData, sequence);
        }
    }
}
