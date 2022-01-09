namespace Disruptor.Processing
{
    public interface IOnBatchStartEvaluator
    {
        bool ShouldInvokeOnBatchStart(long availableSequence, long nextSequence);
    }
}
