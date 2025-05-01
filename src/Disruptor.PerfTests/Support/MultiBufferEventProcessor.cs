using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support;

public class MultiBufferEventProcessor<T>
    where T : class
{
    private readonly ManualResetEventSlim _runEvent = new();
    private volatile int _isRunning;
    private readonly RingBuffer<T>[] _providers;
    private readonly SequenceBarrier[] _barriers;
    private readonly IEventHandler<T> _handler;
    private readonly Sequence[] _sequences;
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
    }

    public Task StartLongRunning()
    {
        return Task.Factory.StartNew(Run, TaskCreationOptions.LongRunning);
    }

    public void WaitUntilStarted(TimeSpan timeout)
    {
        _runEvent.Wait();
    }

    public void Run()
    {
        if (Interlocked.Exchange(ref _isRunning, 1) != 0)
            throw new ApplicationException("Already running");

        _runEvent.Set();

        foreach (var barrier in _barriers)
        {
            barrier.ResetProcessing();
        }

        var barrierLength = _barriers.Length;

        while (true)
        {
            try
            {
                for (var i = 0; i < barrierLength; i++)
                {
                    var waitResult = _barriers[i].WaitForPublishedSequence(-1);
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
            catch (OperationCanceledException) when (_barriers[0].IsCancellationRequested)
            {
                if (_isRunning == 0)
                    break;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                break;
            }
        }
    }

    public long Count => _count;

    public Sequence[] GetSequences()
    {
        return _sequences;
    }

    public void Halt()
    {
        _isRunning = 0;
        _runEvent.Reset();
        _barriers[0].CancelProcessing();
    }

    public bool IsRunning => _isRunning == 1;
}
