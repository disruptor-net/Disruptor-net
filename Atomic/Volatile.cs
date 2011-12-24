using System.Runtime.CompilerServices;

namespace System.Threading
{
    ///<summary>
    /// A small toolkit of classes that support lock-free thread-safe programming on single variables.
    ///</summary>
    public static class Volatile
    {
        /// <summary>
        /// An integer value that may be updated atomically
        /// </summary>
        public struct Integer
        {
            private int _value;

            /// <summary>
            /// Create a new <see cref="Integer"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Integer(int value)
            {
                _value = value;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadUnfenced()
            {
                return _value;
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadAcquireFence()
            {
                var value = _value;
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public int ReadFullFence()
            {
                var value = _value;
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public int ReadCompilerOnlyFence()
            {
                return _value;
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(int newValue)
            {
                _value = newValue;
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(int newValue)
            {
                _value = newValue;
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void WriteCompilerOnlyFence(int newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(int newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(int newValue, int comparand)
            {
                return Interlocked.CompareExchange(ref _value, newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public int AtomicExchange(int newValue)
            {
                return Interlocked.Exchange(ref _value, newValue);
            }

            /// <summary>
            /// Atomically add the given value to the current value and return the sum
            /// </summary>
            /// <param name="delta">The value to be added</param>
            /// <returns>The sum of the current value and the given value</returns>
            public int AtomicAddAndGet(int delta)
            {
                return Interlocked.Add(ref _value, delta);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The incremented value.</returns>
            public int AtomicIncrementAndGet()
            {
                return Interlocked.Increment(ref _value);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The decremented value.</returns>
            public int AtomicDecrementAndGet()
            {
                return Interlocked.Decrement(ref _value);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }
        }

        /// <summary>
        /// A long value that may be updated atomically
        /// </summary>
        public struct Long
        {
            private long _value;

            /// <summary>
            /// Create a new <see cref="Long"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Long(long value)
            {
                _value = value;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadUnfenced()
            {
                return _value;
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadAcquireFence()
            {
                var value = _value;
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public long ReadFullFence()
            {
                Thread.MemoryBarrier();
                return _value;
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public long ReadCompilerOnlyFence()
            {
                return _value;
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(long newValue)
            {
                Thread.MemoryBarrier();
                _value = newValue;
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(long newValue)
            {
                Thread.MemoryBarrier();
                _value = newValue;
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void WriteCompilerOnlyFence(long newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(long newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(long newValue, long comparand)
            {
                return Interlocked.CompareExchange(ref _value, newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public long AtomicExchange(long newValue)
            {
                return Interlocked.Exchange(ref _value, newValue);
            }

            /// <summary>
            /// Atomically add the given value to the current value and return the sum
            /// </summary>
            /// <param name="delta">The value to be added</param>
            /// <returns>The sum of the current value and the given value</returns>
            public long AtomicAddAndGet(long delta)
            {
                return Interlocked.Add(ref _value, delta);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The incremented value.</returns>
            public long AtomicIncrementAndGet()
            {
                return Interlocked.Increment(ref _value);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <returns>The decremented value.</returns>
            public long AtomicDecrementAndGet()
            {
                return Interlocked.Decrement(ref _value);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }
        }

        /// <summary>
        /// A boolean value that may be updated atomically
        /// </summary>
        public struct Boolean
        {
            // bool stored as an int, CAS not available on bool
            private int _value;
            private const int False = 0;
            private const int True = 1;

            /// <summary>
            /// Create a new <see cref="Boolean"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Boolean(bool value)
            {
                _value = value ? True : False;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public bool ReadUnfenced()
            {
                return ToBool(_value);
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public bool ReadAcquireFence()
            {
                var value = ToBool(_value);
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public bool ReadFullFence()
            {
                var value = ToBool(_value);
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public bool ReadCompilerOnlyFence()
            {
                return ToBool(_value);
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(bool newValue)
            {
                var newValueInt = ToInt(newValue);
                Thread.MemoryBarrier();
                _value = newValueInt;
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(bool newValue)
            {
                var newValueInt = ToInt(newValue);
                Thread.MemoryBarrier();
                _value = newValueInt;
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void WriteCompilerOnlyFence(bool newValue)
            {
                _value = ToInt(newValue);
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(bool newValue)
            {
                _value = ToInt(newValue);
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(bool newValue, bool comparand)
            {
                var newValueInt = ToInt(newValue);
                var comparandInt = ToInt(comparand);

                return Interlocked.CompareExchange(ref _value, newValueInt, comparandInt) == comparandInt;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public bool AtomicExchange(bool newValue)
            {
                var newValueInt = ToInt(newValue);
                var originalValue = Interlocked.Exchange(ref _value, newValueInt);
                return ToBool(originalValue);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }

            private static bool ToBool(int value)
            {
                if (value != False && value != True)
                {
                    throw new ArgumentOutOfRangeException("value");
                }

                return value == True;
            }

            private static int ToInt(bool value)
            {
                return value ? True : False;
            }
        }

        /// <summary>
        /// A reference that may be updated atomically
        /// </summary>
        public struct Reference<T> where T : class
        {
            private T _value;

            /// <summary>
            /// Create a new <see cref="Reference{T}"/> with the given initial value.
            /// </summary>
            /// <param name="value">Initial value</param>
            public Reference(T value)
            {
                _value = value;
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <returns>The current value</returns>
            public T ReadUnfenced()
            {
                return _value;
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public T ReadAcquireFence()
            {
                var value = _value;
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <returns>The current value</returns>
            public T ReadFullFence()
            {
                T value = _value;
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <returns>The current value</returns>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public T ReadCompilerOnlyFence()
            {
                return _value;
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(T newValue)
            {
                Thread.MemoryBarrier();
                _value = newValue;
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(T newValue)
            {
                Thread.MemoryBarrier();
                _value = newValue;
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="newValue">The new value</param>
            
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void WriteCompilerOnlyFence(T newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(T newValue)
            {
                _value = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <returns></returns>
            public bool AtomicCompareExchange(T newValue, T comparand)
            {
                return Interlocked.CompareExchange(ref _value, newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <returns>The original value</returns>
            public T AtomicExchange(T newValue)
            {
                return Interlocked.Exchange(ref _value, newValue);
            }

            /// <summary>
            /// Returns the string representation of the current value.
            /// </summary>
            /// <returns>the string representation of the current value.</returns>
            public override string ToString()
            {
                var value = ReadFullFence();
                return value.ToString();
            }
        }

        /// <summary>
        /// An integer array that may be updated atomically
        /// </summary>
        public class IntegerArray
        {
            private readonly int[] _array;

            /// <summary>
            /// Create a new <see cref="IntegerArray"/> of a given length
            /// </summary>
            /// <param name="length">Length of the array</param>
            public IntegerArray(int length)
            {
                if (length <= 0) throw new ArgumentOutOfRangeException("length");

                _array = new int[length];
            }

            /// <summary>
            ///  Create a new AtomicIntegerArray with the same length as, and all elements copied from, the given array.
            /// </summary>
            /// <param name="array"></param>
            public IntegerArray(int[] array)
            {
                if (array == null) throw new ArgumentNullException("array");

                _array = new int[array.Length];
                array.CopyTo(_array, 0);
            }

            /// <summary>
            /// Length of the array
            /// </summary>
            public int Length
            {
                get { return _array.Length; }
            }

            /// <summary>
            /// Read the value without applying any fence
            /// </summary>
            /// <param name="index">The index of the element.</param>
            /// <returns>The current value.</returns>
            public int ReadUnfenced(int index)
            {
                return _array[index];
            }

            /// <summary>
            /// Read the value applying acquire fence semantic
            /// </summary>
            /// <param name="index">The element index</param>
            /// <returns>The current value</returns>
            public int ReadAcquireFence(int index)
            {
                var value = _array[index];
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying full fence semantic
            /// </summary>
            /// <param name="index">The element index</param>
            /// <returns>The current value</returns>
            public int ReadFullFence(int index)
            {
                var value = _array[index];
                Thread.MemoryBarrier();
                return value;
            }

            /// <summary>
            /// Read the value applying a compiler only fence, no CPU fence is applied
            /// </summary>
            /// <param name="index">The element index</param>
            /// <returns>The current value</returns>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public int ReadCompilerOnlyFence(int index)
            {
                return _array[index];
            }

            /// <summary>
            /// Write the value applying release fence semantic
            /// </summary>
            /// <param name="index">The element index</param>
            /// <param name="newValue">The new value</param>
            public void WriteReleaseFence(int index, int newValue)
            {
                _array[index] = newValue;
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Write the value applying full fence semantic
            /// </summary>
            /// <param name="index">The element index</param>
            /// <param name="newValue">The new value</param>
            public void WriteFullFence(int index, int newValue)
            {
                _array[index] = newValue;
                Thread.MemoryBarrier();
            }

            /// <summary>
            /// Write the value applying a compiler fence only, no CPU fence is applied
            /// </summary>
            /// <param name="index">The element index</param>
            /// <param name="newValue">The new value</param>
            [MethodImpl(MethodImplOptions.NoOptimization)]
            public void WriteCompilerOnlyFence(int index, int newValue)
            {
                _array[index] = newValue;
            }

            /// <summary>
            /// Write without applying any fence
            /// </summary>
            /// <param name="index">The index.</param>
            /// <param name="newValue">The new value</param>
            public void WriteUnfenced(int index, int newValue)
            {
                _array[index] = newValue;
            }

            /// <summary>
            /// Atomically set the value to the given updated value if the current value equals the comparand
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="comparand">The comparand (expected value)</param>
            /// <param name="index">The index.</param>
            /// <returns>The original value</returns>
            public bool AtomicCompareExchange(int index, int newValue, int comparand)
            {
                return Interlocked.CompareExchange(ref _array[index], newValue, comparand) == comparand;
            }

            /// <summary>
            /// Atomically set the value to the given updated value
            /// </summary>
            /// <param name="newValue">The new value</param>
            /// <param name="index">The index.</param>
            /// <returns>The original value</returns>
            public int AtomicExchange(int index, int newValue)
            {
                return Interlocked.Exchange(ref _array[index], newValue);
            }

            /// <summary>
            /// Atomically add the given value to the current value and return the sum
            /// </summary>
            /// <param name="delta">The value to be added</param>
            /// <param name="index">The index.</param>
            /// <returns>The sum of the current value and the given value</returns>
            public int AtomicAddAndGet(int index, int delta)
            {
                return Interlocked.Add(ref _array[index], delta);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <param name="index">The index.</param>
            /// <returns>The incremented value.</returns>
            public int AtomicIncrementAndGet(int index)
            {
                return Interlocked.Increment(ref _array[index]);
            }

            /// <summary>
            /// Atomically increment the current value and return the new value
            /// </summary>
            /// <param name="index">The index.</param>
            /// <returns>The decremented value.</returns>
            public int AtomicDecrementAndGet(int index)
            {
                return Interlocked.Decrement(ref _array[index]);
            }
        }
    }
}
