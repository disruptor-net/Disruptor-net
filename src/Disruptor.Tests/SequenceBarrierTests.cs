using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public abstract class SequenceBarrierTests : IDisposable
{
    private readonly RingBuffer<StubEvent> _ringBuffer;
    private readonly CursorFollower _cursorFollower;

    protected SequenceBarrierTests(ISequencer sequencer)
    {
        _ringBuffer = new RingBuffer<StubEvent>(() => new StubEvent(-1), sequencer);
        _cursorFollower = CursorFollower.StartNew(_ringBuffer);
        _ringBuffer.AddGatingSequences(_cursorFollower.Sequence);
    }

    public void Dispose()
    {
        _cursorFollower.Dispose();
    }

    [Test]
    public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsAhead()
    {
        const int expectedNumberMessages = 10;
        const int expectedWorkSequence = 9;
        FillRingBuffer(expectedNumberMessages);

        var sequence1 = new Sequence(expectedNumberMessages);
        var sequence2 = new Sequence(expectedWorkSequence);
        var sequence3 = new Sequence(expectedNumberMessages);

        var sequenceBarrier = _ringBuffer.NewBarrier(sequence1, sequence2, sequence3);

        var completedWorkSequence = sequenceBarrier.WaitForPublishedSequence(expectedWorkSequence).UnsafeAvailableSequence;
        Assert.That(completedWorkSequence >= expectedWorkSequence);
    }

    [Test]
    public void ShouldWaitForWorkCompleteWhereAllWorkersAreBlockedOnRingBuffer()
    {
        const long expectedNumberMessages = 10;
        FillRingBuffer(expectedNumberMessages);

        var workers = new StubEventProcessor[3];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = new StubEventProcessor(expectedNumberMessages - 1);
        }

        var dependencyBarrier = _ringBuffer.NewBarrier(DisruptorUtil.GetSequencesFor(workers));

        Task.Run(() =>
        {
            var sequence = _ringBuffer.Next();
            _ringBuffer[sequence].Value = (int)sequence;
            _ringBuffer.Publish(sequence);

            foreach (var stubWorker in workers)
            {
                stubWorker.Sequence.SetValue(sequence);
            }
        });

        const long expectedWorkSequence = expectedNumberMessages;
        var completedWorkSequence = dependencyBarrier.WaitForPublishedSequence(expectedNumberMessages).UnsafeAvailableSequence;
        Assert.That(completedWorkSequence >= expectedWorkSequence);
    }

    [Test]
    public void ShouldInterruptDuringBusySpin()
    {
        const long expectedNumberMessages = 10;
        FillRingBuffer(expectedNumberMessages);

        var sequence1 = new Sequence(8L);
        var sequence2 = new Sequence(8L);
        var sequence3 = new Sequence(8L);

        var sequenceBarrier = _ringBuffer.NewBarrier(sequence1, sequence2, sequence3);
        var startedSignal = new ManualResetEventSlim();
        var cancellationTokenSource = new CancellationTokenSource();

        var alerted = false;
        var task = Task.Run(() =>
        {
            startedSignal.Set();
            try
            {
                sequenceBarrier.WaitForPublishedSequence(expectedNumberMessages - 1, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                alerted = true;
            }
        });

        startedSignal.Wait();

        Thread.Sleep(200);

        cancellationTokenSource.Cancel();
        sequenceBarrier.CancelProcessing();
        task.Wait();

        Assert.That(alerted, Is.True, "Thread was not interrupted");
    }

    [Test]
    public void ShouldWaitForWorkCompleteWhereCompleteWorkThresholdIsBehind()
    {
        const long expectedNumberMessages = 10;
        FillRingBuffer(expectedNumberMessages);

        var eventProcessors = new StubEventProcessor[3];
        for (var i = 0; i < eventProcessors.Length; i++)
        {
            eventProcessors[i] = new StubEventProcessor(expectedNumberMessages - 2);
        }

        var eventProcessorBarrier = _ringBuffer.NewBarrier(DisruptorUtil.GetSequencesFor(eventProcessors));

        Task.Factory.StartNew(() =>
        {
            foreach (var stubWorker in eventProcessors)
            {
                stubWorker.Sequence.SetValue(stubWorker.Sequence.Value + 1);
            }
        }).Wait();

        const long expectedWorkSequence = expectedNumberMessages - 1;
        var completedWorkSequence = eventProcessorBarrier.WaitForPublishedSequence(expectedWorkSequence).UnsafeAvailableSequence;
        Assert.That(completedWorkSequence >= expectedWorkSequence);
    }

    private void FillRingBuffer(long expectedNumberEvents)
    {
        for (var i = 0; i < expectedNumberEvents; i++)
        {
            var sequence = _ringBuffer.Next();
            var @event = _ringBuffer[sequence];
            @event.Value = i;
            _ringBuffer.Publish(sequence);
        }
    }

    private class StubEventProcessor : IEventProcessor
    {
        private readonly Sequence _sequence = new();
        private readonly EventProcessorState _state = new(restartable: true);

        public StubEventProcessor(long sequence)
        {
            _sequence.SetValue(sequence);
        }

        public Task Start(TaskScheduler taskScheduler, TaskCreationOptions taskCreationOptions)
        {
            var runState = _state.Start();
            taskScheduler.ScheduleAndStart(() => Run(runState), taskCreationOptions);

            return runState.StartTask;
        }

        private void Run(EventProcessorState.RunState runState)
        {
            runState.OnStarted();
            runState.OnShutdown();
        }

        public bool IsRunning => _state.IsRunning;

        public Sequence Sequence => _sequence;

        public Task Halt()
        {
            var runState = _state.Halt();
            return runState.ShutdownTask;
        }
    }
}
