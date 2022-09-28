using System;
using System.Diagnostics;
using System.Threading;
using Disruptor.Dsl;

namespace Disruptor.Samples;

/// <summary>
/// Generate backpressure using a small ring buffer and a slow consumer.
/// </summary>
public static class BackpressureSample
{
    private static readonly Stopwatch _stopwatch = new Stopwatch();

    public static void Main(string[] args)
    {
        var disruptor = new Disruptor<Event>(() => new Event(), ringBufferSize: 4);

        // disruptor.HandleEventsWith(new SlowHandler());
        disruptor.HandleEventsWith(new EarlyReleaserSlowHandler());

        disruptor.Start();

        // Publishes events (fills the ring buffer and then blocks until ring buffer events are available).
        PublishEvents(10);

        Log("Pausing publication");

        Thread.Sleep(TimeSpan.FromSeconds(4));

        // Publishes events (fills the ring buffer and then blocks until ring buffer events are available).
        PublishEvents(10);

        disruptor.Shutdown(TimeSpan.FromSeconds(4.1));

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();

        void PublishEvents(int count)
        {
            for (var i = 0; i < count; i++)
            {
                Log($"PUB before Next");

                _stopwatch.Restart();
                var sequence = disruptor.RingBuffer.Next();
                _stopwatch.Stop();
                try
                {
                    Log($"PUB after Next ({sequence}), Wait: {_stopwatch.Elapsed}");

                    // Configure event
                    disruptor.RingBuffer[sequence].Id = Guid.NewGuid();
                }
                finally
                {
                    disruptor.RingBuffer.Publish(sequence);

                    Log($"PUB after Publish ({sequence})");
                }
            }
        }
    }

    private static void Log(string s) => Console.WriteLine($"{DateTime.UtcNow:HH:mm:ss.fff} {s}");

    private class SlowHandler : IEventHandler<Event>
    {
        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
            Log($"SUB begin {sequence}");

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Log($"SUB end {sequence}");
        }
    }

    /// <summary>
    /// Explicitly releases events after processing, so that events are available for next publication.
    /// </summary>
    private class EarlyReleaserSlowHandler : IEventHandler<Event>, IEventProcessorSequenceAware
    {
        private ISequence _sequenceCallback;

        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
            Log($"SUB begin {sequence}");

            Thread.Sleep(TimeSpan.FromSeconds(1));

            Log($"SUB end {sequence}");

            _sequenceCallback.SetValue(sequence);
        }

        public void SetSequenceCallback(ISequence sequenceCallback)
        {
            _sequenceCallback = sequenceCallback;
        }
    }

    private class Event
    {
        public Guid Id { get; set; }
    }
}
