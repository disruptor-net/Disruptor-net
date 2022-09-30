using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Disruptor.PerfTests.Support;
using Disruptor.Util;
using HdrHistogram;

namespace Disruptor.PerfTests.External;

public class PingPongChannelLatencyTest : ILatencyTest, IExternalTest
{
    private const int _bufferSize = 1024;
    private const long _iterations = 100 * 1000 * 30;
    private const long _pauseDurationInNano = 1000;

    private readonly Channel<PerfEvent> _pingChannel;
    private readonly Channel<PerfEvent> _pongChannel;
    private readonly QueuePinger _pinger;
    private readonly QueuePonger _ponger;

    public PingPongChannelLatencyTest()
    {
        _pingChannel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        _pongChannel = Channel.CreateBounded<PerfEvent>(new BoundedChannelOptions(_bufferSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true,
        });
        _pinger = new QueuePinger(_pingChannel, _pongChannel, _iterations, _pauseDurationInNano);
        _ponger = new QueuePonger(_pingChannel, _pongChannel);
    }

    public void Run(Stopwatch stopwatch, HistogramBase histogram)
    {
        var globalSignal = new CountdownEvent(3);

        _pinger.Reset(globalSignal, histogram);
        _ponger.Reset(globalSignal);

        var pingerTask = _pinger.Start();
        var pongerTask = _ponger.Start();

        globalSignal.Signal();
        globalSignal.Wait();

        stopwatch.Start();
        pingerTask.Wait();
        pongerTask.Wait();
        stopwatch.Stop();
    }

    public int RequiredProcessorCount => 2;

    private class QueuePinger
    {
        private readonly Channel<PerfEvent> _pingChannel;
        private readonly Channel<PerfEvent> _pongChannel;
        private readonly long _maxEvents;
        private readonly long _pauseTimeInNano;
        private readonly double _pauseDurationInStopwatchTicks;

        private HistogramBase _histogram;
        private CountdownEvent _globalSignal;

        public QueuePinger(Channel<PerfEvent> pingChannel, Channel<PerfEvent> pongChannel, long maxEvents, long pauseTimeInNano)
        {
            _pingChannel = pingChannel;
            _pongChannel = pongChannel;
            _maxEvents = maxEvents;
            _pauseTimeInNano = pauseTimeInNano;
            _pauseDurationInStopwatchTicks = StopwatchUtil.GetTimestampFromNanoseconds(pauseTimeInNano);
        }

        private async Task Run()
        {
            _globalSignal.Signal();
            _globalSignal.Wait();

            Thread.Sleep(1000);
            long counter = 0;

            while (counter < _maxEvents)
            {
                var t0 = Stopwatch.GetTimestamp();
                await _pingChannel.Writer.WriteAsync(new PerfEvent { Value = 1 }).ConfigureAwait(false);
                await _pongChannel.Reader.ReadAsync().ConfigureAwait(false);
                var t1 = Stopwatch.GetTimestamp();

                counter++;

                _histogram.RecordValueWithExpectedInterval(StopwatchUtil.ToNanoseconds(t1 - t0), _pauseTimeInNano);

                while (_pauseDurationInStopwatchTicks > (Stopwatch.GetTimestamp() - t1))
                {
                    Thread.Sleep(0);
                }
            }

            await _pingChannel.Writer.WriteAsync(new PerfEvent { Value = -1 }).ConfigureAwait(false);
        }

        public void Reset(CountdownEvent globalSignal, HistogramBase histogram)
        {
            _globalSignal = globalSignal;
            _histogram = histogram;
        }

        public Task Start()
        {
            return Task.Run(Run);
        }
    }

    public class QueuePonger
    {
        private readonly Channel<PerfEvent> _pingChannel;
        private readonly Channel<PerfEvent> _pongChannel;
        private CountdownEvent _globalSignal;

        public QueuePonger(Channel<PerfEvent> pingChannel, Channel<PerfEvent> pongChannel)
        {
            _pingChannel = pingChannel;
            _pongChannel = pongChannel;
        }

        private async Task Run()
        {
            _globalSignal.Signal();
            _globalSignal.Wait();

            while (await _pingChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_pingChannel.Reader.TryRead(out var perfEvent))
                {
                    if (perfEvent.Value == -1)
                        return;

                    await _pongChannel.Writer.WriteAsync(perfEvent).ConfigureAwait(false);
                }
            }
        }

        public void Reset(CountdownEvent globalSignal)
        {
            _globalSignal = globalSignal;
        }

        public Task Start()
        {
            return Task.Run(Run);
        }
    }
}
