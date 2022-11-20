namespace Disruptor.Processing;

public interface IBatchSizeLimiter
{
    long ApplyMaxBatchSize(long availableSequence, long nextSequence);
}
