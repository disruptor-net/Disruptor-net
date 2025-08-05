using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Disruptor.Processing;
using Disruptor.Util;

namespace Disruptor.Benchmarks;

public class SequenceBarrierBenchmarksV2 : SequenceBarrierBenchmarks, IDisposable
{
    private readonly SequenceBarrier _requesterSequenceBarrier;
    private readonly SequenceBarrier _replierSequenceBarrier;
    private readonly CancellationTokenSource _replierCancellationTokenSource;
    private readonly Task _replierTask;
    private readonly ManualResetEventSlim _replierStarted = new();

    public SequenceBarrierBenchmarksV2()
    {
        _requesterSequenceBarrier = _requesterSequencer.NewBarrier(null, _requesterSequencer.GetCursorSequence());
        _replierSequenceBarrier = _replierSequencer.NewBarrier(null, _replierSequencer.GetCursorSequence());
        _replierCancellationTokenSource = new CancellationTokenSource();

        _replierTask = Task.Run(RunReplier);
        _replierStarted.Wait();
    }

    public void Dispose()
    {
        _replierCancellationTokenSource.Cancel();
        _replierSequenceBarrier.CancelProcessing();
        _replierTask.Wait();
    }

    [Benchmark(OperationsPerInvoke = OperationsPerInvoke)]
    public void Run()
    {
        Run(new EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader((MultiProducerSequencer)_requesterSequenceBarrier.Sequencer));
    }

    private void Run<T>(T publishedSequenceReader)
        where T : struct, IPublishedSequenceReader
    {
        for (var i = 0; i < OperationsPerInvoke; i++)
        {
            var seq = _replierRingBuffer.Next();
            _replierRingBuffer[seq].Value = 123456;
            _replierRingBuffer.Publish(seq);

            SequenceWaitResult result;
            do
            {
                result = _requesterSequenceBarrier.WaitFor(seq);
            }
            while (result.IsTimeout || publishedSequenceReader.GetHighestPublishedSequence(seq, result.UnsafeAvailableSequence) < seq);

            BeforePublication();
        }
    }

    private void RunReplier()
    {
        RunReplier(new EventProcessorHelpers.MultiProducerSequencerPublishedSequenceReader((MultiProducerSequencer)_replierSequenceBarrier.Sequencer));
    }

    private void RunReplier<T>(T publishedSequenceReader)
        where T : struct, IPublishedSequenceReader
    {
        _replierStarted.Set();

        var cancellationToken = _replierCancellationTokenSource.Token;

        try
        {
            var sequence = -1L;

            while (true)
            {
                sequence++;

                SequenceWaitResult result;
                do
                {
                    result = _replierSequenceBarrier.WaitFor(sequence, cancellationToken);
                }
                while (result.IsTimeout || publishedSequenceReader.GetHighestPublishedSequence(sequence, result.UnsafeAvailableSequence) < sequence);

                BeforePublication();

                var seq = _requesterRingBuffer.Next();
                _requesterRingBuffer[seq].Value = 123456;
                _requesterRingBuffer.Publish(seq);
            }
        }
        catch (OperationCanceledException e)
        {
            Console.WriteLine(e);
        }
    }
}
