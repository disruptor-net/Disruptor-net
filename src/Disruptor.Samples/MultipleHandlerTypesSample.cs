using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;
using HdrHistogram;

namespace Disruptor.Samples;

public class MultipleHandlerTypesSample
{
    public static void Main(string[] args)
    {
        var disruptor = new Disruptor<Event>(() => new Event(), 1024, new AsyncWaitStrategy());

        disruptor.HandleEventsWith(new CalculatorHandler()) // IEventHandler
                 .Then(new PersisterHandler()) // IAsyncBatchEventHandler
                 .Then(new MetricHandler()) // IEventHandler
                 .Then(new ResetHandler()); // IBatchEventHandler

        disruptor.Start();

        Console.WriteLine("Running...");

        var ringBuffer = disruptor.RingBuffer;
        var random = new Random();

        for (var i = 0; i < 10 * 1000 * 1000; i++)
        {
            var timestamp = Stopwatch.GetTimestamp();

            using var scope = ringBuffer.PublishEvent();

            var data = scope.Event();
            data.Id = i % 8;
            data.Price = 5 + 2 * random.NextDouble();
            data.Metrics.BeforeAcquireTimestamp = timestamp;
        }

        disruptor.Shutdown(TimeSpan.FromSeconds(30));

        Thread.Sleep(1000);

        Console.WriteLine("Press Enter to exit...");
        Console.ReadLine();
    }

    private static void WriteState<THandler>(object state)
    {
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };
        var stateText = JsonSerializer.Serialize(state, jsonSerializerOptions);
        Console.WriteLine($"{typeof(THandler).Name} state: {stateText}");
    }

    public class CalculatorHandler : IEventHandler<Event>
    {
        private int _eventCount;
        private double _outputSum;

        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
            _eventCount++;
            data.Metrics.BeforeCalculationTimestamp = Stopwatch.GetTimestamp();

            var outputValue = data.Price * 1.1 + 42;

            data.OutputValue = outputValue;

            _outputSum += outputValue;
        }

        public void OnShutdown()
        {
            WriteState<CalculatorHandler>(new
            {
                EventCount = _eventCount,
                OutputSum = _outputSum,
            });
        }
    }

    public class PersisterHandler : IAsyncBatchEventHandler<Event>
    {
        private readonly Persister _persister = new();
        private int _savedItemCount;
        private double _outputSum;

        public async ValueTask OnBatch(EventBatch<Event> batch, long sequence)
        {
            var first = batch[0]; // OnBatch is never invoked with empty batches
            first.Metrics.SaveBatchSize = batch.Length;
            first.Metrics.BeforeSaveTimestamp = Stopwatch.GetTimestamp();

            var outputs = batch.AsEnumerable().Select(x => x.Output).ToList();

            foreach (var output in outputs)
            {
                _outputSum += output.Value;
            }

            _savedItemCount += outputs.Count;

            await _persister.Save(outputs);

            first.Metrics.AfterSaveTimestamp = Stopwatch.GetTimestamp();
        }

        public void OnShutdown()
        {
            WriteState<PersisterHandler>(new
            {
                SavedItemCount = _savedItemCount,
                OutputSum = _outputSum,
            });
        }
    }

    public class MetricHandler : IEventHandler<Event>
    {
        private readonly LongHistogram _queueingHistogram = new(1_000_000, 4);
        private readonly LongHistogram _saveHistogram = new(1_000_000, 4);
        private long _savedItemCount;
        private int _saveBatchCount;
        private double _outputSum;
        private int _gen0Count;
        private int _gen1Count;
        private int _gen2Count;

        public void OnStart()
        {
            _gen0Count = GC.CollectionCount(0);
            _gen1Count = GC.CollectionCount(1);
            _gen2Count = GC.CollectionCount(2);
        }

        public void OnEvent(Event data, long sequence, bool endOfBatch)
        {
            _outputSum += data.Output.Value;
            _queueingHistogram.RecordValue(GetMicroseconds(data.Metrics.BeforeAcquireTimestamp, data.Metrics.BeforeCalculationTimestamp));

            if (data.Metrics.SaveBatchSize == 0)
                return;

            _saveBatchCount++;
            _savedItemCount += data.Metrics.SaveBatchSize;
            _saveHistogram.RecordValue(GetMicroseconds(data.Metrics.BeforeSaveTimestamp, data.Metrics.AfterSaveTimestamp));
        }

        private static long GetMicroseconds(long from, long to)
        {
            var timeSpanTicks = (to - from) * (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
            return (long)(timeSpanTicks / 10);
        }

        public void OnShutdown()
        {
            var gen0Count = GC.CollectionCount(0);
            var gen1Count = GC.CollectionCount(1);
            var gen2Count = GC.CollectionCount(2);

            WriteState<MetricHandler>(new
            {
                SavedItemCount = _savedItemCount,
                OutputSum = _outputSum,
                SaveAverageBatchSize = (_savedItemCount / _saveBatchCount),
                SaveLatency = $"[P50: {_saveHistogram.GetValueAtPercentile(50)} us, P90: {_saveHistogram.GetValueAtPercentile(90)} us]",
                QueueingLatency = $"[P50: {_queueingHistogram.GetValueAtPercentile(50)} us, P90: {_queueingHistogram.GetValueAtPercentile(90)} us]",
                GC = $"{gen0Count - _gen0Count} - {gen1Count - _gen1Count} - {gen2Count - _gen2Count}",
            });
        }
    }

    public class ResetHandler : IBatchEventHandler<Event>
    {
        public void OnBatch(EventBatch<Event> batch, long sequence)
        {
            foreach (var data in batch.AsSpan())
            {
                data.Id = default;
                data.Price = default;
                data.OutputValue = default;
                data.Metrics.SaveBatchSize = default;
                data.Metrics.BeforeSaveTimestamp = default;
                data.Metrics.AfterSaveTimestamp = default;
            }
        }
    }

    public class Event
    {
        public int Id { get; set; }
        public double Price { get; set; }
        public double OutputValue { get; set; }
        public Metrics Metrics { get; } = new();

        public Output Output => new(Id, OutputValue);
    }

    public readonly record struct Output(int Id, double Value);

    public class Metrics
    {
        public int SaveBatchSize { get; set; }
        public long BeforeAcquireTimestamp { get; set; }
        public long BeforeCalculationTimestamp { get; set; }
        public long BeforeSaveTimestamp { get; set; }
        public long AfterSaveTimestamp { get; set; }
    }

    public class Persister
    {
        public async Task Save(IList<Output> outputs)
        {
            await Task.Yield();
        }
    }
}
