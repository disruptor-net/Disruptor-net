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

    public void Add(IEventProcessor eventProcessor, object eventHandler, DependentSequenceGroup dependentSequences)
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

    public Sequence[] GetLastSequenceInChain(bool includeStopped)
    {
        var lastSequence = new List<Sequence>();
        foreach (var consumerInfo in _consumerInfos)
        {
            if ((includeStopped || consumerInfo.IsRunning) && consumerInfo.IsEndOfChain)
            {
                var sequences = consumerInfo.Sequences;
                lastSequence.AddRange(sequences);
            }
        }

        return lastSequence.ToArray();
    }

    public IEventProcessor GetEventProcessorFor(object eventHandler)
    {
        var found = _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo);
        if(!found)
        {
            throw new ArgumentException("The event handler " + eventHandler + " is not processing events.");
        }

        return eventProcessorInfo!.EventProcessor;
    }

    public Sequence GetSequenceFor(object eventHandler)
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

    public DependentSequenceGroup? GetDependentSequencesFor(object eventHandler)
    {
        return _eventProcessorInfoByEventHandler.TryGetValue(eventHandler, out var eventProcessorInfo) ? eventProcessorInfo.DependentSequences : null;
    }

    public IEnumerator<IConsumerInfo> GetEnumerator() => _consumerInfos.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
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
