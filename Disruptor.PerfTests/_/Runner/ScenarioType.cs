namespace Disruptor.PerfTests.Runner
{
    public enum ScenarioType
    {
        All = 0,
        UniCast1P1C = 1,
        MultiCast1P3C = 2,
        Pipeline3Step = 3,
        Sequencer3P1C = 4,
        DiamondPath1P3C = 5,
        Pipeline3StepLatency = 6,
        UniCast1P1CBatch = 7
    }
}