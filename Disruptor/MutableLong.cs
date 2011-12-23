namespace Disruptor
{
    /// <summary>
    /// Holder class for a long value.
    /// </summary>
    public class MutableLong
    {
        /// <summary>
        /// Internal value
        /// </summary>
        public long Value { get; set; }

        ///<summary>
        /// Create a new instance of a mutable long
        ///</summary>
        ///<param name="value"></param>
        public MutableLong(long value)
        {
            Value = value;
        }
    }
}