using Disruptor.Tests.Support;

namespace Disruptor.PerfTests.Support
{
    public class EventCountingWorkHandler : IWorkHandler<ValueEvent>
    {
        private readonly PaddedLong[] _counters;
        private readonly int _index;

        public EventCountingWorkHandler(PaddedLong[] counters, int index)
        {
            _counters = counters;
            _index = index;
        }

        public void OnEvent(ValueEvent evt)
        {
            _counters[_index].Value = _counters[_index].Value + 1L;
        }
    }
}