using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Disruptor.Processing;

namespace Disruptor.Dsl;

internal class ConsumerRepository : IEnumerable<IConsumerInfo>
{
    private readonly Dictionary<object, EventProcessorInfo> _eventProcessorInfoByEventHandler;
    private readonly Dictionary<Sequence, IConsumerInfo> _eventProcessorInfoBySequence;
    private readonly List<IConsumerInfo> _consumerInfos = new();

    public ConsumerRepository()
    {
        _eventProcessorInfoByEventHandler = new Dictionary<object, EventProcessorInfo>(new IdentityComparer<object>());
        _eventProcessorInfoBySequence = new Dictionary<Sequence, IConsumerInfo>(new IdentityComparer<Sequence>());
    }

    public void Add(IEventProcessor eventProcessor, object eventHandler)
    {
        var consumerInfo = new EventProcessorInfo(eventProcessor, eventHandler);
        _eventProcessorInfoByEventHandler[eventHandler] = consumerInfo;
        _eventProcessorInfoBySequence[eventProcessor.Sequence] = consumerInfo;
        _consumerInfos.Add(consumerInfo);
    }

    public void Add(IEventProcessor processor)
    {
        var consumerInfo = new EventProcessorInfo(processor, null);
        _eventProcessorInfoBySequence [processor.Sequence] = consumerInfo;
        _consumerInfos.Add(consumerInfo);
    }

    public void Add<T>(WorkerPool<T> workerPool)
        where T : class
    {
        var workerPoolInfo = new WorkerPoolInfo<T>(workerPool);
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

    public IEventProcessor GetEventProcessor(object eventHandler)
    {
        return GetEventProcessorInfo(eventHandler).EventProcessor;
    }

    public Sequence GetSequence(object eventHandler)
    {
        return GetEventProcessor(eventHandler).Sequence;
    }

    public int GetEventHandlerGroupPosition(object eventHandler)
    {
        return GetEventProcessorInfo(eventHandler).DependentSequences.EventHandlerGroupPosition;
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

    public DependentSequenceGroup? GetDependentSequencesOrNull(object eventHandler)
    {
        return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo)
            ? eventProcessorInfo.DependentSequences
            : null;
    }

    public IEnumerator<IConsumerInfo> GetEnumerator() => _consumerInfos.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    private EventProcessorInfo GetEventProcessorInfo(object eventHandler)
    {
        return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo)
            ? eventProcessorInfo
            : throw new ArgumentException("The event handler is not registered.");
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
