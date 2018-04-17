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
        private static readonly int _offsetToArrayData = ElemOffset(new object[1]);

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Read<T>(object array, int index)
        {
            IL.DeclareLocals(
                false,
                typeof(byte).MakeByRefType()
            );

            Ldarg(nameof(array)); // load the object
            Stloc_0(); // convert the object pointer to a byref
            Ldloc_0(); // load the object pointer as a byref

            Ldarg(nameof(index)); // load the index
            Sizeof(typeof(object)); // get the size of the object pointer
            Mul(); // multiply the index by the offset size of the object pointer

            Ldsfld(new FieldRef(typeof(Util), nameof(_offsetToArrayData))); // get the offset to the start of the array
            Add(); // add the start offset to the element offset

            Add(); // add the start + offset to the byref object pointer

            Ldobj(typeof(T)); // load a T value from the computed address

            return IL.Return<T>();
        }

        private static int ElemOffset(object[] arr)
        {
            Ldarg(nameof(arr));
            Ldc_I4_0();
            Ldelema(typeof(object));
            Ldarg(nameof(arr));
            Sub();

            return IL.Return<int>();
        }
    }
}
