using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Disruptor.Util;

public static class TimerResolutionUtil
{
#if NET
    [DllImport("WINMM.dll", ExactSpelling = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows")]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("WINMM.dll", ExactSpelling = true), DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [SupportedOSPlatform("windows")]
    private static extern uint timeEndPeriod(uint uPeriod);

    public static IDisposable SetTimerResolution(uint period)
    {
        if (!OperatingSystem.IsWindows())
            return TimerResolutionScope.None;

        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period), "Invalid timer resolution");

        var result = timeBeginPeriod(period);
        return result != 0 ? new TimerResolutionScope(period) : TimerResolutionScope.None;
    }

    private class TimerResolutionScope : IDisposable
    {
        public static TimerResolutionScope None { get; } = new(0);

        private uint _period;

        public TimerResolutionScope(uint period)
        {
            _period = period;
        }

        public void Dispose()
        {
            if (_period != 0)
            {
#pragma warning disable CA1416
                timeEndPeriod(_period);
                _period = 0;
#pragma warning restore CA1416
            }
        }
    }
#endif
}
