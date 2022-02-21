﻿using Disruptor.Dsl;
using Disruptor.Processing;

namespace Disruptor;

/// <summary>
/// Callback interface to be implemented for processing events as they become available in the <see cref="ValueRingBuffer{T}"/>
/// </summary>
/// <typeparam name="T">Type of events for sharing during exchange or parallel coordination of an event.</typeparam>
/// <remarks>
/// See <see cref="Dsl.ValueDisruptor{T,TRingBuffer}.SetDefaultExceptionHandler"/> or <see cref="ValueDisruptor{T,TRingBuffer}.HandleExceptionsFor"/>
/// if you want to handle exceptions propagated out of the handler.
/// </remarks>
public interface IValueEventHandler<T>
    where T : struct
{
    /// <summary>
    /// Called when a publisher has committed an event to the <see cref="ValueRingBuffer{T}"/>. The <see cref="IValueEventProcessor{T}"/> will
    /// read messages from the <see cref="ValueRingBuffer{T}"/> in batches, where a batch is all of the events available to be
    /// processed without having to wait for any new event to arrive.  This can be useful for event handlers that need
    /// to do slower operations like I/O as they can group together the data from multiple events into a single
    /// operation.  Implementations should ensure that the operation is always performed when endOfBatch is true as
    /// the time between that message an the next one is indeterminate.
    /// </summary>
    /// <param name="data">Data committed to the <see cref="ValueRingBuffer{T}"/></param>
    /// <param name="sequence">Sequence number committed to the <see cref="ValueRingBuffer{T}"/></param>
    /// <param name="endOfBatch">flag to indicate if this is the last event in a batch from the <see cref="ValueRingBuffer{T}"/></param>
    void OnEvent(ref T data, long sequence, bool endOfBatch);

    /// <summary>
    /// Called on each batch start before the first call to <see cref="IEventHandler{T}.OnEvent"/>.
    /// </summary>
    /// <param name="batchSize">the batch size.</param>
    void OnBatchStart(long batchSize)
    {
    }

    ///<summary>
    /// Called once on thread start before first event is available.
    ///</summary>
    void OnStart()
    {
    }

    /// <summary>
    /// Called once just before the thread is shutdown.
    /// </summary>
    /// <remarks>
    /// Sequence event processing will already have stopped before this method is called. No events will
    /// be processed after this message.
    /// </remarks>
    void OnShutdown()
    {
    }

    /// <summary>
    /// Invoked when the wait strategy timeouts.
    /// </summary>
    /// <remarks>
    /// This only happens if the current wait strategy can return timeouts (e.g.: <see cref="TimeoutBlockingWaitStrategy"/>).
    /// </remarks>
    void OnTimeout(long sequence)
    {
    }
}
