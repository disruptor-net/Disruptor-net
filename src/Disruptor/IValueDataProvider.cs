namespace Disruptor
{
    public interface IValueDataProvider<T>
    {
        ref T this[long sequence] { get; }
    }
}