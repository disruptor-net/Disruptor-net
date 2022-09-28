using System;

namespace Disruptor.Dsl;

/// <summary>
/// Factory methods for sequencers.
/// </summary>
public static class SequencerFactory
{
    /// <summary>
    /// Default <see cref="ProducerType"/> of disruptors.
    /// </summary>
    public static readonly ProducerType DefaultProducerType = ProducerType.Multi;

    /// <summary>
    /// Creates default <see cref="IWaitStrategy"/>.
    /// </summary>
    /// <returns></returns>
    public static IWaitStrategy DefaultWaitStrategy()
    {
        return new BlockingWaitStrategy();
    }

    /// <summary>
    /// Create a new sequencer with the specified producer type and <see cref="DefaultWaitStrategy"/>.
    /// </summary>
    /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static ISequencer Create(ProducerType producerType, int bufferSize)
    {
        return Create(producerType, bufferSize, DefaultWaitStrategy());
    }

    /// <summary>
    /// Create a new sequencer with the specified producer type.
    /// </summary>
    /// <param name="producerType">producer type to use <see cref="ProducerType" /></param>
    /// <param name="bufferSize">number of elements to create within the ring buffer.</param>
    /// <param name="waitStrategy">used to determine how to wait for new elements to become available.</param>
    /// <returns>a constructed ring buffer.</returns>
    /// <exception cref="ArgumentOutOfRangeException">if the producer type is invalid</exception>
    /// <exception cref="ArgumentException">if bufferSize is less than 1 or not a power of 2</exception>
    public static ISequencer Create(ProducerType producerType, int bufferSize, IWaitStrategy waitStrategy)
    {
        switch (producerType)
        {
            case ProducerType.Single:
                return new SingleProducerSequencer(bufferSize, waitStrategy);
            case ProducerType.Multi:
                return new MultiProducerSequencer(bufferSize, waitStrategy);
            default:
                throw new ArgumentOutOfRangeException(producerType.ToString());
        }
    }
}
