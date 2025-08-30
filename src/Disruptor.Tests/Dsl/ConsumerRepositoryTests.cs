using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using Disruptor.Processing;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl;

[TestFixture]
public class ConsumerRepositoryTests
{
    private readonly ConsumerRepository _consumerRepository = new();
    private readonly EventProcessor _eventProcessor1 = new();
    private readonly EventProcessor _eventProcessor2 = new();
    private readonly DummyEventHandler<TestEvent> _handler1 = new();
    private readonly DependentSequenceGroup _dependentSequenceGroup1 = new(new Sequence());

    [Test]
    public void ShouldGetDependentSequenceGroupByHandler()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);

        Assert.That(_consumerRepository.GetDependentSequencesFor(_handler1), Is.SameAs(_dependentSequenceGroup1));
    }

    [Test]
    public void ShouldReturnNullForDependentSequenceGroupWhenHandlerIsNotRegistered()
    {
        Assert.That(_consumerRepository.GetDependentSequencesFor(_handler1), Is.Null);
    }

    [Test]
    public void ShouldRetrieveEventProcessorForHandler()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);
        Assert.That(_consumerRepository.GetEventProcessorFor(_handler1), Is.SameAs(_eventProcessor1));
    }

    [Test]
    public void ShouldThrowExceptionWhenHandlerIsNotRegistered()
    {
        Assert.Throws<ArgumentException>(() => _consumerRepository.GetEventProcessorFor(new SleepingEventHandler()));
    }

    [Test]
    public async Task ShouldStartAndHaltProcessors()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);

        await _consumerRepository.StartAll(TaskScheduler.Default);

        Assert.That(_eventProcessor1.IsRunning, Is.True);

        await _consumerRepository.HaltAll();

        Assert.That(_eventProcessor1.IsRunning, Is.False);
    }

    [Test]
    public void ShouldDisposeOwnedEventProcessorOnDispose()
    {
        _consumerRepository.AddOwnedProcessor(_eventProcessor1, _handler1, _dependentSequenceGroup1);
        _consumerRepository.Add(_eventProcessor1, owned: false);

        _consumerRepository.DisposeAll();

        Assert.That(_eventProcessor1.IsDisposed, Is.True);
        Assert.That(_eventProcessor2.IsDisposed, Is.False);
    }

    private class EventProcessor : IEventProcessor
    {
        private readonly EventProcessorState _state = new(new DummySequenceBarrier(), restartable: true);

        public Sequence Sequence { get; } = new();

        public Task Halt() => _state.Halt();

        public void Dispose() => _state.Dispose();

        public Task Start(TaskScheduler taskScheduler)
        {
            var runState = _state.Start();
            taskScheduler.StartLongRunningTask(() => Run(runState));
            return runState.StartTask;
        }

        public bool IsRunning => _state.IsRunning;

        public bool IsDisposed => _state.IsDisposed;

        private void Run(EventProcessorState.RunState runState)
        {
            runState.OnStarted();

            var spinWait = new SpinWait();
            while (!runState.CancellationToken.IsCancellationRequested)
            {
                spinWait.SpinOnce();
            }

            runState.OnShutdown();
        }
    }
}
