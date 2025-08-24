using System;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Processing;

namespace Disruptor.PerfTests.Support;

public class MultiBufferEventProcessor<T>
    where T : class
{
    private readonly RingBuffer<T>[] _providers;
    private readonly SequenceBarrier[] _barriers;
    private readonly IEventHandler<T> _handler;
    private readonly Sequence[] _sequences;
    private readonly EventProcessorState _state;
    private long _count;

    public MultiBufferEventProcessor(RingBuffer<T>[] providers, SequenceBarrier[] barriers, IEventHandler<T> handler)
    {
        if (providers.Length != barriers.Length)
            throw new ArgumentException();

        _providers = providers;
        _barriers = barriers;
        _handler = handler;

        _sequences = new Sequence[providers.Length];
        for (var i = 0; i < _sequences.Length; i++)
        {
            _sequences[i] = new Sequence();
        }

        _state = new EventProcessorState(new MultiBufferSequenceBarrier(barriers), restartable: true);
    }

    public Task StartLongRunning()
    {
        var runState = _state.Start();
        Task.Factory.StartNew(() => Run(runState), TaskCreationOptions.LongRunning);

        return runState.StartTask;
    }

    private void Run(EventProcessorState.RunState runState)
    {
        runState.OnStarted();

        var cancellationToken = runState.CancellationToken;
        var barriers = _barriers;

        while (true)
        {
            try
            {
                for (var i = 0; i < barriers.Length; i++)
                {
                    var waitResult = barriers[i].WaitForPublishedSequence(-1, cancellationToken);
                    if (waitResult.IsTimeout)
                        continue;

                    var available = waitResult.UnsafeAvailableSequence;
                    var sequence = _sequences[i];
                    var dataProvider = _providers[i];

                    var nextSequence = sequence.Value + 1;
                    for (var s = nextSequence; s <= available; s++)
                    {
                        _handler.OnEvent(dataProvider[s], s, s == available);
                    }

                    sequence.SetValue(available);

                    _count += available - nextSequence + 1;
                }

                Thread.Yield();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                break;
            }
        }

        runState.OnShutdown();
    }

    public long Count => _count;

    public Sequence[] GetSequences()
    {
        return _sequences;
    }

    public Task Halt()
    {
        return _state.Halt();
    }

    public bool IsRunning => _state.IsRunning;

    private class MultiBufferSequenceBarrier(SequenceBarrier[] barriers) : ICancellableBarrier
    {
        public void Dispose()
        {
            foreach (var barrier in barriers)
            {
                barrier.Dispose();
            }
        }

        public void CancelProcessing()
        {
            foreach (var barrier in barriers)
            {
                barrier.CancelProcessing();
            }
        }
    }
}
