using System;
using System.IO;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.PerfTests.Support;
using Disruptor.Processing;
using Disruptor.Tests.IpcPublisher;

namespace Disruptor.PerfTests.Throughput.OneToOne.ValueEventHandler;

/// <summary>
/// Unicast a series of items between 1 publisher and 1 event processor.
/// Use <see cref="UnmanagedRingBuffer{T}"/>.
///
/// +----+    +-----+
/// | P1 |--->| EP1 |
/// +----+    +-----+
///
/// Disruptor:
/// ==========
///              track to prevent wrap
///              +------------------+
///              |                  |
///              |                  v
/// +----+    +====+    +====+   +-----+
/// | P1 |---\| RB |/---| SB |   | EP1 |
/// +----+    +====+    +====+   +-----+
///      claim       get   ^        |
///                        |        |
///                        +--------+
///                          waitFor
///
/// P1  - Publisher 1
/// RB  - RingBuffer
/// SB  - SequenceBarrier
/// EP1 - EventProcessor 1
/// </summary>
public class OneToOneSequencedThroughputTest_Ipc : IThroughputTest, IDisposable
{
    private const int _bufferSize = 1024 * 64;
    private const long _iterations = 1000L * 1000L * 100L;

    private readonly ProgramOptions _options;
    private readonly IpcRingBuffer<PerfValueEvent> _ringBuffer;
    private readonly AdditionEventHandler _eventHandler;
    private readonly long _expectedResult = PerfTestUtil.AccumulatedAddition(_iterations);
    private readonly PerfTestIpcEventProcessor<PerfValueEvent> _eventProcessor;

    public OneToOneSequencedThroughputTest_Ipc(ProgramOptions options)
    {
        _options = options;
        var memory = IpcRingBufferMemory.CreateTemporary<PerfValueEvent>(_bufferSize);
        _ringBuffer = new IpcRingBuffer<PerfValueEvent>(memory, (IIpcWaitStrategy)options.GetWaitStrategy(), true);
        _eventHandler = new AdditionEventHandler(options.GetCustomCpu(1));
        _eventProcessor = _ringBuffer.CreatePerfTestEventProcessor(_eventHandler);
    }

    public int RequiredProcessorCount => 2;

    public void Dispose()
    {
        _ringBuffer.Dispose();
    }

    public long Run(ThroughputSessionContext sessionContext)
    {
        long expectedCount = _eventProcessor.SequencePointer.Value + _iterations;

        _eventHandler.Reset(expectedCount);
        var startTask = _eventProcessor.Start();
        startTask.Wait(TimeSpan.FromSeconds(5));

        var mutexName = $"Ipc-{Path.GetRandomFileName()}";
        using var mutex = new Mutex(true, mutexName);

        var publisherCpu = _options.GetCustomCpu(0);

        var publisher = RemoteIpcPublisher.Start(
            command: "throughput-test",
            commandArguments: $"--ipc-directory-path \"{_ringBuffer.IpcDirectoryPath}\" --iterations {_iterations} --mutex-name \"{mutexName}\" --cpu \"{publisherCpu}\"",
            ipcPublisherPath: _options.IpcPublisherPath
        );

        Thread.Sleep(500);

        sessionContext.Start();

        mutex.ReleaseMutex();

        _eventHandler.WaitForSequence();
        sessionContext.Stop();
        PerfTestUtil.WaitForEventProcessorSequence(expectedCount, _eventProcessor.SequencePointer);

        publisher.WaitForExit(2000);

        PerfTestUtil.FailIfNot(publisher.ExitCode, 0, $"Publisher should have exited cleanly, but was: {publisher.ExitCode}");

        var shutdownTask = _eventProcessor.Halt();
        shutdownTask.Wait(2000);

        sessionContext.SetBatchData(_eventHandler.BatchesProcessed, _iterations);

        PerfTestUtil.FailIfNot(_expectedResult, _eventHandler.Value, $"Handler should have processed {_expectedResult} events, but was: {_eventHandler.Value}");

        return _iterations;
    }
}
