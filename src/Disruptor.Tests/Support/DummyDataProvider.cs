namespace Disruptor.Tests.Support
{
    public class DummyDataProvider<T> : IDataProvider<T>, IValueDataProvider<T>
    {
        private T _value;

        public T this[long sequence] => default(T);

        ref T IValueDataProvider<T>.this[long sequence]
        {
            get
            {
                _value = default(T);
                return ref _value;
            }
        }
    }
}
