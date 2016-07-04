namespace Disruptor
{
    public interface IDataProvider<T>
    {
        T Get(long sequence);
    }
}