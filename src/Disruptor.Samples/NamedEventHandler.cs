using System.Threading;

namespace Disruptor.Samples
{
    public class NamedEventHandler<T> : IEventHandler<T>
    {
        private readonly string _name;
        private string _oldName;

        public NamedEventHandler(string name)
        {
            _name = name;
        }

        public void OnEvent(T data, long sequence, bool endOfBatch)
        {
        }

        public void OnStart()
        {
            var currentThread = Thread.CurrentThread;
            _oldName = currentThread.Name;
            currentThread.Name = _name;
        }

        public void OnShutdown()
        {
            Thread.CurrentThread.Name = _oldName;
        }
    }
}
