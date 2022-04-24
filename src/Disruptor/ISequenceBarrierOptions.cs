namespace Disruptor;

public interface ISequenceBarrierOptions
{
    internal struct None : ISequenceBarrierOptions
    {
    }

    internal struct IsDependentSequencePublished : ISequenceBarrierOptions
    {
    }

    public static ISequenceBarrierOptions Get(ISequencer sequencer, DependentSequenceGroup dependentSequences)
    {
        if (sequencer is SingleProducerSequencer)
        {
            // The SingleProducerSequencer increments the cursor sequence on publication so the cursor/ sequence
            // is always published.
            return new IsDependentSequencePublished();
        }

        if (!dependentSequences.DependsOnCursor)
        {
            // When the sequence barrier does not directly depend on the ring buffer cursor, the dependent sequence
            // is always published (the value is derived from other event processors which cannot process unpublished
            // sequences).
            return new IsDependentSequencePublished();
        }

        return new None();
    }
}
