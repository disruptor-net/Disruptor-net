using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Disruptor.Util;

public static class StopwatchUtil
{
    private static readonly double _tickFrequency = (double)10_000_000 / Stopwatch.Frequency;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetTimestampFromMicroseconds(long microseconds)
    {
        return (long)(10 * microseconds / _tickFrequency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long GetTimestampFromNanoseconds(long microseconds)
    {
        return (long)(microseconds / 100.0 / _tickFrequency);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToNanoseconds(long timestamp)
    {
        return (long)(100 * (timestamp * _tickFrequency));
    }
}
