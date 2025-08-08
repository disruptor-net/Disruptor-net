using System;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests.Support;

/// <summary>
/// Exposes a sequence that is regularly updated to follow the target cursor.
/// </summary>
public class CursorFollower : IDisposable
{
    private readonly ICursored _sequencer;
    private volatile bool _running;
    private Task? _runTask;

    private CursorFollower(ICursored sequencer, SequencePointer sequencePointer)
    {
        _sequencer = sequencer;
        SequencePointer = sequencePointer;
    }

    public Sequence Sequence { get; } = new();
    public SequencePointer SequencePointer { get; }

    public static CursorFollower StartNew(ICursored sequencer)
    {
        var cursorFollower = new CursorFollower(sequencer, default);
        cursorFollower.Start();

        return cursorFollower;
    }

    public static CursorFollower StartNew<T>(IpcRingBuffer<T> sequencer)
        where T : unmanaged
    {
        var cursorFollower = new CursorFollower(sequencer, sequencer.NewSequence());
        cursorFollower.Start();

        return cursorFollower;
    }

    private void Start()
    {
        _running = true;
        _runTask = Task.Factory.StartNew(Run, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current);

        void Run()
        {
            var spinWait = new SpinWait();

            while (_running)
            {
                var cursor = _sequencer.Cursor;
                if (cursor <= Sequence.Value)
                {
                    spinWait.SpinOnce();
                }
                else
                {
                    spinWait.Reset();
                    Sequence.SetValue(cursor);
                    if (SequencePointer != default)
                        SequencePointer.SetValue(cursor);
                }
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _runTask!.Wait();
    }
}
