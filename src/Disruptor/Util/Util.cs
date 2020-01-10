using System;
using System.Runtime.CompilerServices;
using InlineIL;
using static InlineIL.IL.Emit;

namespace Disruptor
{
    /// <summary>
    /// Set of common functions used by the Disruptor
    /// </summary>
    internal static class Util
    {
        /// <summary>
        /// Ring buffer padding size in bytes.
        ///
        /// The padding should be added at the beginning and at the end of the
        /// ring buffer arrays.
        ///
        /// Used to avoid false sharing.
        /// </summary>
        public const int RingBufferPaddingBytes = 128;

        /// <summary>
        /// Gets the ring buffer padding as a number of events.
        /// </summary>
        /// <param name="eventSize"></param>
        /// <returns></returns>
        public static int GetRingBufferPaddingEventCount(int eventSize)
        {
            return (int)Math.Ceiling((double)RingBufferPaddingBytes / eventSize);
        }

        /// <summary>
        /// Calculate the next power of 2, greater than or equal to x.
        /// </summary>
        /// <param name="x">Value to round up</param>
        /// <returns>The next power of 2 from x inclusive</returns>
        public static int CeilingNextPowerOfTwo(this int x)
        {
            var result = 2;

            while (result < x)
            {
                result <<= 1;
            }

            return result;
        }

        /// <summary>
        /// Calculate the log base 2 of the supplied integer, essentially reports the location
        /// of the highest bit.
        /// </summary>
        /// <param name="i">Value to calculate log2 for.</param>
        /// <returns>The log2 value</returns>
        public static int Log2(int i)
        {
            var r = 0;
            while ((i >>= 1) != 0)
            {
                ++r;
            }

            return r;
        }

        /// <summary>
        /// Test whether a given integer is a power of 2
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static bool IsPowerOf2(this int x)
        {
            return x > 0 && (x & (x - 1)) == 0;
        }

        /// <summary>
        /// Get the minimum sequence from an array of <see cref="Sequence"/>s.
        /// </summary>
        /// <param name="sequences">sequences to compare.</param>
        /// <param name="minimum">an initial default minimum.  If the array is empty this value will returned.</param>
        /// <returns>the minimum sequence found or lon.MaxValue if the array is empty.</returns>
        public static long GetMinimumSequence(ISequence[] sequences, long minimum = long.MaxValue)
        {
            for (var i = 0; i < sequences.Length; i++)
            {
                var sequence = sequences[i].Value;
                minimum = Math.Min(minimum, sequence);
            }

            return minimum;
        }

        /// <summary>
        /// Get an array of <see cref="Sequence"/>s for the passed <see cref="IEventProcessor"/>s
        /// </summary>
        /// <param name="processors">processors for which to get the sequences</param>
        /// <returns>the array of <see cref="Sequence"/>s</returns>
        public static ISequence[] GetSequencesFor(params IEventProcessor[] processors)
        {
            var sequences = new ISequence[processors.Length];
            for (int i = 0; i < sequences.Length; i++)
            {
                sequences[i] = processors[i].Sequence;
            }

            return sequences;
        }

        //
        // The offset from an array to the start of the array data is assumed to be 8 bytes for BIT32 and 16 bytes for BIT64 (= 2 x sizeof(object)).
        // This offset was previously computed and stored in a static readonly field but in NetCore this field access introduces
        // a method call that has a strong negative impact on performance.
        //
        // Read<T> returns the element @ address = array + array_data_offset + index x sizeof(object)
        //                                       = array + (index + 2) x sizeof(object)
        //
        // ReadValue<T> returns the element @ address = array + array_data_offset + index x sizeof(T)
        //                                            = array + sizeof(object) + sizeof(object) + index x sizeof(T)
        //

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(object array, int index)
            where T : class
        {
            IL.DeclareLocals(false, typeof(byte).MakeByRefType());

            Ldarg(nameof(array));
            Stloc_0(); // convert the object pointer to a byref
            Ldloc_0(); // load the object pointer as a byref

            Ldarg(nameof(index));
            Ldc_I4_2();
            Add(); // index + 2

            Sizeof(typeof(object));
            Mul(); // (index + 2) x sizeof(object)

            Add(); // array + (index + 2) x sizeof(object)

            Ldobj(typeof(T)); // load a T value from the computed address

            return IL.Return<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadValue<T>(object array, int index)
            where T : struct
        {
            IL.DeclareLocals(false, typeof(byte).MakeByRefType());

            Ldarg(nameof(array));
            Stloc_0(); // convert the object pointer to a byref
            Ldloc_0(); // load the object pointer as a byref

            Ldarg(nameof(index));
            Sizeof(typeof(T));
            Mul(); // index x sizeof(T)

            Sizeof(typeof(object));
            Add(); // index x sizeof(T) +  sizeof(object)

            Sizeof(typeof(object));
            Add(); // index x sizeof(T) +  sizeof(object) +  sizeof(object)

            Add(); // array + index x sizeof(T) +  sizeof(object) +  sizeof(object)

            Ret();

            throw IL.Unreachable();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ref T ReadValue<T>(IntPtr pointer, int index, int size)
            where T : struct
        {
            IL.DeclareLocals(false, typeof(byte).MakeByRefType());

            Ldarg(nameof(pointer));
            Stloc_0(); // convert the object pointer to a byref
            Ldloc_0(); // load the object pointer as a byref

            Ldarg(nameof(index));
            Ldarg(nameof(size));
            Mul(); // index x size

            Add(); // pointer + index x size

            Ret();

            throw IL.Unreachable();
        }

        public static int SizeOf<T>()
            where T : struct
        {
            Sizeof(typeof(T));

            return IL.Return<int>();
        }
    }
}
