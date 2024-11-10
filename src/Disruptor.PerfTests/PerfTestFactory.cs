using System;

namespace Disruptor.PerfTests;

public static class PerfTestFactory
{
    public static bool TryCreateLatencyTest(Type testType, ProgramOptions options, out ILatencyTest latencyTest)
        => TryCreateTest(testType, options, out latencyTest);

    public static bool TryCreateThroughputTest(Type testType, ProgramOptions options, out IThroughputTest throughputTest)
        => TryCreateTest(testType, options, out throughputTest);

    private static bool TryCreateTest<T>(Type testType, ProgramOptions options, out T test)
    {
        if (!typeof(T).IsAssignableFrom(testType))
        {
            Console.Error.WriteLine($"Error: the test type is invalid, FullName: {testType.FullName}");
            test = default;
            return false;
        }

        if (testType.GetConstructor([typeof(ProgramOptions)]) is { } constructor)
        {
            test = (T)constructor.Invoke([options]);
            return true;
        }

        test = (T)Activator.CreateInstance(testType);
        return test != null;
    }

    public static bool CheckProcessorsRequirements(this ILatencyTest latencyTest, ProgramOptions programOptions)
        => CheckProcessorsRequirements(programOptions, latencyTest.RequiredProcessorCount);

    public static bool CheckProcessorsRequirements(this IThroughputTest latencyTest, ProgramOptions programOptions)
        => CheckProcessorsRequirements(programOptions, latencyTest.RequiredProcessorCount);

    private static bool CheckProcessorsRequirements(ProgramOptions programOptions, int requiredProcessorCount)
    {
        var availableProcessors = Environment.ProcessorCount;
        if (requiredProcessorCount > availableProcessors)
        {
            Console.Error.WriteLine("Error: your system has insufficient processors to execute the test efficiently.");
            Console.Error.WriteLine($"Processors required = {requiredProcessorCount}, available = {availableProcessors}");
            return false;
        }

        if (requiredProcessorCount > programOptions.CpuSet.Length)
        {
            Console.Error.WriteLine("Error: the CPU set is two small to execute the test efficiently.");
            Console.Error.WriteLine($"CPU count required = {requiredProcessorCount}, CPU set length = {programOptions.CpuSet.Length}");
            return false;
        }

        return true;
    }
}
