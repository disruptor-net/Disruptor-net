using System;
using System.Diagnostics.CodeAnalysis;

namespace Disruptor.PerfTests;

public class PerfTestType
{
    public PerfTestType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        Type = type;
    }

    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
    public Type Type { get; }

    public string Name => Type.Name;
    public string FullName => Type.FullName;
    public string Namespace => Type.Namespace;

    public static PerfTestType[] GeAll() =>
    [
        new(typeof(Latency.OneWay.OneWayAwaitLatencyTest_ManualResetValueTaskSourceCore)),
        new(typeof(Latency.OneWay.OneWayAwaitLatencyTest_QueueUserWorkItem)),
        new(typeof(Latency.OneWay.OneWayAwaitLatencyTest_TaskCompletionSource)),
        new(typeof(Latency.OneWay.OneWayChannelLatencyTest)),
        new(typeof(Latency.OneWay.OneWaySequencedLatencyTest)),
        new(typeof(Latency.OneWay.OneWaySequencedLatencyTest_AsyncBatchHandler)),
        new(typeof(Latency.OneWay.OneWaySequencedLatencyTest_BatchHandler)),
        new(typeof(Latency.OneWay.OneWaySequencedLatencyTest_Channel)),
        new(typeof(Latency.PingPong.PingPongAwaitLatencyTest_ManualResetValueTaskSourceCore)),
        new(typeof(Latency.PingPong.PingPongAwaitLatencyTest_QueueUserWorkItem)),
        new(typeof(Latency.PingPong.PingPongAwaitLatencyTest_TaskCompletionSource)),
        new(typeof(Latency.PingPong.PingPongBlockingCollectionLatencyTest)),
        new(typeof(Latency.PingPong.PingPongChannelLatencyTest)),
        new(typeof(Latency.PingPong.PingPongSequencedLatencyTest)),
        new(typeof(Latency.PingPong.PingPongSequencedLatencyTest_AsyncBatchHandler)),
        new(typeof(Latency.PingPong.PingPongSequencedLatencyTest_BatchHandler)),
        new(typeof(Latency.PingPong.PingPongSequencedLatencyTest_Multi)),
        new(typeof(Latency.PingPong.PingPongSequencedLatencyTest_Value)),
        new(typeof(Throughput.OneToOne.AsyncBatchEventHandler.OneToOneSequencedThroughputTest_AsyncBatchHandler)),
        new(typeof(Throughput.OneToOne.AsyncBatchEventHandler.OneToOneSequencedThroughputTest_AsyncBatchHandler_BatchPublisher)),
        new(typeof(Throughput.OneToOne.AsyncBatchEventHandler.OneToOneSequencedThroughputTest_AsyncBatchHandler_Timeout)),
        new(typeof(Throughput.OneToOne.AsyncEventStream.OneToOneSequencedAsyncEventStreamThroughputTest)),
        new(typeof(Throughput.OneToOne.BatchEventHandler.OneToOneSequencedThroughputTest_BatchHandler)),
        new(typeof(Throughput.OneToOne.BatchEventHandler.OneToOneSequencedThroughputTest_BatchHandler_BatchPublisher)),
        new(typeof(Throughput.OneToOne.Channel.OneToOneChannelAsyncThroughputTest)),
        new(typeof(Throughput.OneToOne.Channel.OneToOneChannelAsyncValueThroughputTest)),
        new(typeof(Throughput.OneToOne.Channel.OneToOneChannelThroughputTest)),
        new(typeof(Throughput.OneToOne.ConcurrentQueue.OneToOneBlockingCollectionThroughputTest)),
        new(typeof(Throughput.OneToOne.ConcurrentQueue.OneToOneBlockingCollectionThroughputTest_Value)),
        new(typeof(Throughput.OneToOne.ConcurrentQueue.OneToOneConcurrentQueueThroughputTest)),
        new(typeof(Throughput.OneToOne.ConcurrentQueue.OneToOneConcurrentQueueThroughputTest_Value)),
        new(typeof(Throughput.OneToOne.EventHandler.OneToOneSequencedThroughputTest)),
        new(typeof(Throughput.OneToOne.EventHandler.OneToOneSequencedThroughputTest_BatchPublisher)),
        new(typeof(Throughput.OneToOne.EventHandler.OneToOneSequencedThroughputTest_LongArray)),
        new(typeof(Throughput.OneToOne.EventHandler.OneToOneSequencedThroughputTest_Multi)),
        new(typeof(Throughput.OneToOne.EventPoller.OneToOneSequencedPollerThroughputTest)),
        new(typeof(Throughput.OneToOne.Sequencer.OneToOneRawThroughputTest)),
        new(typeof(Throughput.OneToOne.Sequencer.OneToOneRawThroughputTest_BatchPublisher)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Ipc)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Unmanaged)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Unmanaged_BatchPublisher)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Value)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Value_BatchPublisher)),
        new(typeof(Throughput.OneToOne.ValueEventHandler.OneToOneSequencedThroughputTest_Value_Multi)),
        new(typeof(Throughput.OneToThree.OneToThreeDiamondQueueThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreeDiamondSequencedThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreePipelineQueueThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreePipelineSequencedThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreeReleasingWorkerPoolThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreeSequencedThroughputTest)),
        new(typeof(Throughput.OneToThree.OneToThreeWorkerPoolThroughputTest)),
        new(typeof(Throughput.ThreeToOne.ThreeToOneQueueThroughputTest)),
        new(typeof(Throughput.ThreeToOne.ThreeToOneSequencedThroughputTest)),
        new(typeof(Throughput.ThreeToOne.ThreeToOneSequencedThroughputTest_BatchPublication)),
        new(typeof(Throughput.ThreeToThree.ThreeToThreeSequencedThroughputTest)),
        new(typeof(Throughput.TwoToTwo.TwoToTwoWorkProcessorThroughputTest)),
    ];
}
