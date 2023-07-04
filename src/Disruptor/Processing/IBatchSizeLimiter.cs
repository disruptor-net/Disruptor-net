namespace Disruptor.Processing;

/// <summary>
/// Used by event processors to limit the size of the event batches.
/// </summary>
public interface IBatchSizeLimiter
{
    long ApplyMaxBatchSize(long availableSequence, long nextSequence);
}
