namespace Disruptor.Tests
{
    public class AsyncWaitStrategyTestsWithoutTimeout : AsyncWaitStrategyTests
    {
        protected override AsyncWaitStrategy CreateWaitStrategy()
        {
            return new AsyncWaitStrategy();
        }
    }
}
