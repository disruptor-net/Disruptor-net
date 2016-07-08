using System.Threading;

namespace Disruptor.Tests.Support
{
    public class AtomicReference<T> where T : class
    {
        T _reference;

        public AtomicReference(T reference = null)
        {
            _reference = reference;
        }

        public T Read()
        {
            var reference = _reference;
            Thread.MemoryBarrier();
            return reference;
        }

        public void Write(T value)
        {
            Thread.MemoryBarrier();
            _reference = value;
        }
    }
}