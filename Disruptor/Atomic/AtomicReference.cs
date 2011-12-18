using System.Threading;

namespace Disruptor.Atomic
{
    /// <summary>
    /// An object reference that may be updated atomically. 
    /// </summary>
    /// <typeparam name="T">The type of object referred to by this reference</typeparam>
    public struct AtomicReference<T> where T : class
    {
        private volatile T _value;

        /// <summary>
        /// Creates a new AtomicReference with the given initial value.
        /// </summary>
        /// <param name="value">the initial value</param>
        public AtomicReference(T value)
        {
            _value = value;
        }

        /// <summary>
        /// Atomically sets the value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">the expected value</param>
        /// <param name="update"> the new value</param>
        /// <returns>true if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        public bool CompareAndSet(T expected, T update)
        {
#pragma warning disable 420 // THe interlocked opperation emits fences
            return Interlocked.CompareExchange(ref _value, update, expected) == expected;
#pragma warning restore 420
        }

        /// <summary>
        /// Volatile access to inner value
        /// </summary>
        public T Value
        {
            get { return _value; }
            set { _value = value; }
        }
    }
}
