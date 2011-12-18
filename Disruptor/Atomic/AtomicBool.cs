using System.Threading;

namespace Disruptor.Atomic
{
    ///<summary>
    ///</summary>
    public struct AtomicBool
    {
        private int _value;

        private const int True = 1;
        private const int False = 0;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="initialValue"></param>
        public AtomicBool(bool initialValue)
        {
            _value = initialValue ? True : False;
        }

        ///<summary>
        /// Returns the current value.
        /// Unconditionally sets to the given value.
        ///</summary>
        public bool Value
        {
            get { return Thread.VolatileRead(ref _value) == True; }
            set { Thread.VolatileWrite(ref _value, value ? True : False); }
        }

        /// <summary>
        /// Atomically set the value to the given updated value if the current value == the expected value.
        /// </summary>
        /// <param name="expected">The expected value</param>
        /// <param name="update">The new value</param>
        /// <returns>True if successful. False return indicates that the actual value was not equal to the expected value.</returns>
        public bool CompareAndSet(bool expected, bool update)
        {
            var value = update ? True : False;
            var exp = expected ? True : False;

            return Interlocked.CompareExchange(ref _value, value, exp) == exp;
        }
    }
}
