using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.Dsl;

internal class IpcConsumerRepository<T>
    where T : unmanaged
{
    private readonly Dictionary<IEventHandler, IIpcEventProcessor<T>> _eventProcessorInfoByEventHandler = new(new IdentityComparer<IEventHandler>());
    // private readonly Dictionary<Sequence, IConsumerInfo> _eventProcessorInfoBySequence = new(new IdentityComparer<Sequence>());
    private readonly List<IIpcEventProcessor<T>> _consumerInfos = new();
    private readonly HashSet<IIpcEventProcessor<T>> _endOfChainConsumers = new();

    public IReadOnlyCollection<IIpcEventProcessor<T>> Consumers => _consumerInfos;

    public void Add(IIpcEventProcessor<T> eventProcessor, IEventHandler eventHandler, IpcDependentSequenceGroup dependentSequences)
    {
        _eventProcessorInfoByEventHandler[eventHandler] = eventProcessor;
        _consumerInfos.Add(eventProcessor);
        _endOfChainConsumers.Add(eventProcessor);
    }

    public SequencePointer[] GetGatingSequences()
    {
        return _endOfChainConsumers.Select(x => x.SequencePointer).ToArray();
    }

    public bool HasBacklog(long cursor, bool includeStopped)
    {
        foreach (var consumerInfo in _consumerInfos)
        {
            if ((includeStopped || consumerInfo.IsRunning) && _endOfChainConsumers.Contains(consumerInfo))
            {
                if (cursor > consumerInfo.SequencePointer.Value)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public IIpcEventProcessor<T> GetEventProcessorFor(IEventHandler eventHandler)
    {
        var found = _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessor);
        if(!found)
        {
            throw new ArgumentException("The event handler " + eventHandler + " is not processing events.");
        }

        return eventProcessor!;
    }

    // public Sequence GetSequenceFor(IEventHandler eventHandler)
    // {
    //     return GetEventProcessorFor(eventHandler).Sequence;
    // }

    public void UnMarkEventProcessorsAsEndOfChain(IIpcEventProcessor<T>[] eventProcessors)
    {
        foreach (var eventProcessor in eventProcessors)
        {
            _endOfChainConsumers.Remove(eventProcessor);
        }
    }

    // public DependentSequenceGroup? GetDependentSequencesFor(IEventHandler eventHandler)
    // {
    //     return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo) ? eventProcessorInfo.DependentSequences : null;
    // }

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

    public Task DisposeAll()
    {
        var tasks = new List<Task>(_consumerInfos.Count);

        foreach (var consumerInfo in _consumerInfos)
        {
            var disposeTask = consumerInfo.DisposeAsync();
            if (!disposeTask.IsCompleted)
                tasks.Add(disposeTask.AsTask());
        }

        return Task.WhenAll(tasks);
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
