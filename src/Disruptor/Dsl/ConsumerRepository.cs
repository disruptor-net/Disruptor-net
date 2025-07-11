using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

internal class ConsumerRepository
{
    private readonly Dictionary<IEventHandler, EventProcessorInfo> _eventProcessorInfoByEventHandler = new(new IdentityComparer<IEventHandler>());
    private readonly Dictionary<Sequence, IConsumerInfo> _eventProcessorInfoBySequence = new(new IdentityComparer<Sequence>());
    private readonly List<IConsumerInfo> _consumerInfos = new();

    public IReadOnlyCollection<IConsumerInfo> Consumers => _consumerInfos;

    public void Add(IEventProcessor eventProcessor, IEventHandler eventHandler, DependentSequenceGroup dependentSequences)
    {
        var consumerInfo = new EventProcessorInfo(eventProcessor, eventHandler, dependentSequences);
        _eventProcessorInfoByEventHandler[eventHandler] = consumerInfo;
        _eventProcessorInfoBySequence[eventProcessor.Sequence] = consumerInfo;
        _consumerInfos.Add(consumerInfo);
    }

    public void Add(IEventProcessor processor)
    {
        var consumerInfo = new EventProcessorInfo(processor, null, null);
        _eventProcessorInfoBySequence [processor.Sequence] = consumerInfo;
        _consumerInfos.Add(consumerInfo);
    }

    public void Add<T>(WorkerPool<T> workerPool, DependentSequenceGroup dependentSequences)
        where T : class
    {
        var workerPoolInfo = new WorkerPoolInfo<T>(workerPool, dependentSequences);
        _consumerInfos.Add(workerPoolInfo);
        foreach (var sequence in workerPool.GetWorkerSequences())
        {
            _eventProcessorInfoBySequence[sequence] = workerPoolInfo;
        }
    }

    public bool HasBacklog(long cursor, bool includeStopped)
    {
        foreach (var consumerInfo in _consumerInfos)
        {
            if ((includeStopped || consumerInfo.IsRunning) && consumerInfo.IsEndOfChain)
            {
                foreach (var sequence in consumerInfo.Sequences)
                {
                    if (cursor > sequence.Value)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public IEventProcessor GetEventProcessorFor(IEventHandler eventHandler)
    {
        var found = _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo);
        if(!found)
        {
            throw new ArgumentException("The event handler " + eventHandler + " is not processing events.");
        }

        return eventProcessorInfo!.EventProcessor;
    }

    public Sequence GetSequenceFor(IEventHandler eventHandler)
    {
        return GetEventProcessorFor(eventHandler).Sequence;
    }

    public void UnMarkEventProcessorsAsEndOfChain(params Sequence[] barrierEventProcessors)
    {
        foreach (var barrierEventProcessor in barrierEventProcessors)
        {
            if (_eventProcessorInfoBySequence.TryGetValue(barrierEventProcessor, out var consumerInfo))
            {
                consumerInfo.MarkAsUsedInBarrier();
            }
        }
    }

    public DependentSequenceGroup? GetDependentSequencesFor(IEventHandler eventHandler)
    {
        return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo) ? eventProcessorInfo.DependentSequences : null;
    }

    public Task StartAll(TaskScheduler taskScheduler)
    {
        var startTasks = new List<Task>(_consumerInfos.Count);
        foreach (var consumerInfo in _consumerInfos)
        {
            startTasks.Add(consumerInfo.Start(taskScheduler));
        }

        return Task.WhenAll(startTasks);
    }

    public Task HaltAll()
    {
        var haltTasks = new List<Task>(_consumerInfos.Count);
        foreach (var consumerInfo in _consumerInfos)
        {
            haltTasks.Add(consumerInfo.Halt());
        }

        return Task.WhenAll(haltTasks);
    }

    private class IdentityComparer<TKey> : IEqualityComparer<TKey>
    {
        public bool Equals(TKey? x, TKey? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(TKey obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
