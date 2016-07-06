﻿using System;
using System.Threading;

namespace Disruptor
{
    public class TimeoutBlockingWaitStrategy : IWaitStrategy
    {
        private readonly object _gate = new object();
        private readonly TimeSpan _timeout;

        public TimeoutBlockingWaitStrategy(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public long WaitFor(long sequence, Sequence cursor, Sequence dependentSequence, ISequenceBarrier barrier)
        {
            var timeSpan = _timeout;
            if (cursor.Value < sequence) // volatile read
            {
                Monitor.Enter(_gate);
                try
                {
                    while (cursor.Value < sequence) // volatile read
                    {
                        barrier.CheckAlert();
                        if (!Monitor.Wait(_gate, timeSpan))
                            throw TimeoutException.Instance;
                    }
                }
                finally
                {
                    Monitor.Exit(_gate);
                }
            }

            long availableSequence;
            while ((availableSequence = dependentSequence.Value) < sequence)
            {
                barrier.CheckAlert();
            }

            return availableSequence;
        }

        public void SignalAllWhenBlocking()
        {
            lock (_gate)
            {
                Monitor.PulseAll(_gate);
            }
        }
    }
}