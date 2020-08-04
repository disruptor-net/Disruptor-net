namespace Disruptor.Tests.Support
{
    public class ArrayDataProvider<T> : IDataProvider<T>, IValueDataProvider<T>
    {
        public T[] Data { get; }

        public ArrayDataProvider(T[] data)
        {
            Data = data;
        }

        T IDataProvider<T>.this[long sequence] => Data[sequence];

        ref T IValueDataProvider<T>.this[long sequence] => ref Data[sequence];

        public IDataProvider<T> AsDataProvider() => this;
        public IValueDataProvider<T> AsValueDataProvider() => this;
    }
}
