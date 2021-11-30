using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests
{
    public class SequenceUpdater
    {
        public readonly Sequence Sequence = new Sequence();
        private readonly CountdownEvent _barrier = new CountdownEvent(2);
        private readonly int _sleepTime;
        private readonly IWaitStrategy _waitStrategy;

        public SequenceUpdater(long sleepTime, IWaitStrategy waitStrategy)
        {
            _sleepTime = (int)sleepTime;
            _waitStrategy = waitStrategy;
        }

        public Task Start() => Task.Run(() => Run());

        public void Run()
        {
            try
            {
                _barrier.Signal();
                _barrier.Wait();
                if (0 != _sleepTime)
                {
                    Thread.Sleep(_sleepTime);
                }
                Sequence.IncrementAndGet();
                _waitStrategy.SignalAllWhenBlocking();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public void WaitForStartup()
        {
            _barrier.Signal();
            _barrier.Wait();
        }
    }
}
