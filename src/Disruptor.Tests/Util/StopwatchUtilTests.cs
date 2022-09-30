using System.Diagnostics;
using System.Runtime.CompilerServices;
using Disruptor.Util;
using NUnit.Framework;

namespace Disruptor.Tests.Util;

[TestFixture]
public class StopwatchUtilTests
{
    [Test]
    public void ShouldGetTimestampFromMicroseconds()
    {
        var timestamp = StopwatchUtil.GetTimestampFromMicroseconds(123);
        var stopwatch = CreateStopwatch(timestamp);

        var expectedTimeSpanTicks = 123 * 10;
        var elapsedTimeSpanTicks = stopwatch.Elapsed.Ticks;
        Assert.AreEqual(expectedTimeSpanTicks, elapsedTimeSpanTicks);
    }

    [Test]
    public void ShouldGetTimestampFromNanoseconds()
    {
        var timestamp = StopwatchUtil.GetTimestampFromNanoseconds(1000);
        var stopwatch = CreateStopwatch(timestamp);

        var expectedTimeSpanTicks = 1000 / 100;
        var elapsedTimeSpanTicks = stopwatch.Elapsed.Ticks;
        Assert.AreEqual(expectedTimeSpanTicks, elapsedTimeSpanTicks);
    }

    [Test]
    public void ShouldConvertTimestampToNanoseconds()
    {
        var timestamp = 123000;
        var nanoseconds = StopwatchUtil.ToNanoseconds(timestamp);
        var stopwatch = CreateStopwatch(timestamp);

        var expectedNanoseconds = stopwatch.Elapsed.Ticks * 100;
        Assert.AreEqual(expectedNanoseconds, nanoseconds);
    }

    [Test]
    public void ShouldConvertTimestampFromMicrosecondsToNanoseconds()
    {
        var timestamp = StopwatchUtil.GetTimestampFromMicroseconds(123);
        var nanoseconds = StopwatchUtil.ToNanoseconds(timestamp);

        Assert.AreEqual(123 * 1000, nanoseconds);
    }

    private static Stopwatch CreateStopwatch(long elapsedTimestamp)
    {
        var stopwatch = new Stopwatch();
        var stopwatchLayout = Unsafe.As<Stopwatch, StopwatchLayout>(ref stopwatch);
        stopwatchLayout._elapsed = elapsedTimestamp;

        return stopwatch;
    }

    private class StopwatchLayout
    {
#pragma warning disable CS0649
        // ReSharper disable InconsistentNaming
        // ReSharper disable NotAccessedField.Local
        internal long _elapsed;
        internal long _startTimeStamp;
        internal bool _isRunning;
        // ReSharper restore NotAccessedField.Local
        // ReSharper restore InconsistentNaming
#pragma warning restore CS0649
    }
}
