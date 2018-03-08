namespace Disruptor.Tests.Support
{
    public class DummyDataProvider<T> : IDataProvider<T>
    {
        public T this[long sequence] => default(T);
    }
}
