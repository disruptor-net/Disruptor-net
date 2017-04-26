using System;
using System.Threading;

namespace Disruptor
{
    /// <summary>
    /// Represent a <see cref="SpinWait"/> that never calls <code>Thread.Sleep(1)</code>.
    /// </summary>
    public struct AggressiveSpinWait
    {
#if NETSTANDARD2_0
        private static readonly bool _isSingleProcessor = Environment.ProcessorCount == 1;
        private const int _yieldThreshold = 10;
        private const int _sleep0EveryHowManyTimes = 5;
        private int _count;

        private bool NextSpinWillYield => _count > _yieldThreshold || _isSingleProcessor;

        public void SpinOnce()
        {
            if (NextSpinWillYield)
            {
                int yieldsSoFar = (_count >= _yieldThreshold ? _count - _yieldThreshold : _count);

                if ((yieldsSoFar % _sleep0EveryHowManyTimes) == (_sleep0EveryHowManyTimes - 1))
                {
                    Thread.Sleep(0);
                }
                else
                {
                    Thread.Yield();
                }
            }
            else
            {
                Thread.SpinWait(4 << _count);
            }

            _count = (_count == int.MaxValue ? _yieldThreshold : _count + 1);
        }
#else
        private SpinWait _spinWait;

        public void SpinOnce()
        {
            if (_spinWait.NextSpinWillYield)
            {
                Thread.Sleep(0);
                _spinWait.Reset();
            }
            else
            {
                _spinWait.SpinOnce();
            }
        }
#endif
    }
}