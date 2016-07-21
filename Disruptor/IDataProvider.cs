namespace Disruptor
{
    public interface IDataProvider<T>
    {
        T this[long sequence] { get; }
    }
}