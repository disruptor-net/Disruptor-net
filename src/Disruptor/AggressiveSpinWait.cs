using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Provides support for spin-based waiting, without using <code>Thread.Sleep(1)</code>.
/// </summary>
/// <remarks>
/// <para>
/// Using this type is less aggressive than a busy-spin. The use of <code>Thread.Sleep(0)</code> allows the thread to give up
/// its time-slice, thus preventing starvation.
///</para>
/// <para>
/// Using this type is more aggressive than a <see cref="SpinWait"/>. <code>Thread.Sleep(1)</code> is not used to avoid generating pauses
/// that are not acceptable for many low latency use cases.
/// </para>
/// </remarks>
public struct AggressiveSpinWait
{
    internal static readonly int SpinCountForSpinBeforeWait = Environment.ProcessorCount == 1 ? 1 : 35;

#if NETCOREAPP
    private SpinWait _spinWait;

    public int Count => _spinWait.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SpinOnce()
    {
        _spinWait.SpinOnce(-1);
    }
#else
    private static readonly bool _isSingleProcessor = Environment.ProcessorCount == 1;
    private const int _yieldThreshold = 10;
    private const int _sleep0EveryHowManyTimes = 5;
    private int _count;

    private bool NextSpinWillYield => _count > _yieldThreshold || _isSingleProcessor;

    public int Count => _count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
#endif
}
