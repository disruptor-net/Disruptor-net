using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Session;

namespace Disruptor.PerfTests.Diagnostics;

public class PerfCounterSource
{
    public PerfCounterSource(PerfCounter counter, ProfileSourceInfo profileSourceInfo)
    {
        Counter = counter;
        ProfileSourceInfo = profileSourceInfo;
    }

    public PerfCounter Counter { get; }
    public ProfileSourceInfo ProfileSourceInfo { get; }
    public Dictionary<ulong, ulong> PerInstructionPointer { get; } = new(10000);
    public ulong Count { get; private set; }

    public int ProfileSourceId => ProfileSourceInfo.ID;
    public int Interval => Counter.Interval ?? Math.Min(ProfileSourceInfo.MaxInterval, Math.Max(ProfileSourceInfo.MinInterval, ProfileSourceInfo.Interval));

    public void OnSample(ulong instructionPointer)
    {
        Count += (ulong)Interval;

        PerInstructionPointer.TryGetValue(instructionPointer, out var currentValue);
        PerInstructionPointer[instructionPointer] = currentValue + (ulong)Interval;
    }
}
