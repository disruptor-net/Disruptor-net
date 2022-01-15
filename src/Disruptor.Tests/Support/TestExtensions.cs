namespace Disruptor.Tests.Support;

public static class TestExtensions
{
    public static long ComputeHighestPublishedSequence(this ISequencer sequencer)
    {
        return sequencer.GetHighestPublishedSequence(0, sequencer.Cursor);
    }
}