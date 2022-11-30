using Disruptor.Testing.Support;

namespace Disruptor.PerfTests.Support;

public class EventCountingWorkHandler : IWorkHandler<PerfEvent>
{
    private readonly PaddedLong[] _counters;
    private readonly int _index;

    public EventCountingWorkHandler(PaddedLong[] counters, int index)
    {
        _counters = counters;
        _index = index;
    }

    public void OnEvent(PerfEvent evt)
    {
        _counters[_index].Value = _counters[_index].Value + 1L;
    }
}
