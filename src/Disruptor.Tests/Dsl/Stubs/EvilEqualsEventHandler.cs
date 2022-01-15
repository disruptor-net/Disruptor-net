using Disruptor.Tests.Support;

namespace Disruptor.Tests.Dsl.Stubs;

public class EvilEqualsEventHandler : IEventHandler<TestEvent>, IValueEventHandler<TestValueEvent>
{
    public void OnEvent(TestEvent data, long sequence, bool endOfBatch)
    {
    }

    public void OnEvent(ref TestValueEvent data, long sequence, bool endOfBatch)
    {
    }

    public override bool Equals(object? obj)
    {
        return true;
    }

    public override int GetHashCode()
    {
        return 1;
    }
}