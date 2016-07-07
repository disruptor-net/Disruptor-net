using System;
using System.Threading;
using Atomic;

namespace Disruptor
{
    /// <summary>
    /// Provides static methods for managing a <see cref="SequenceGroup"/> object
    /// </summary>
    internal static class SequenceGroups
    {
        public static void AddSequences(ref Volatile.Reference<Sequence[]> sequences, ICursored cursor, params Sequence[] sequencesToAdd) 
        {
            long cursorSequence;
            Sequence[] updatedSequences;
            Sequence[] currentSequences;

            do
            {
                currentSequences = sequences.ReadFullFence();
                updatedSequences = new Sequence[currentSequences.Length + sequencesToAdd.Length];
                Array.Copy(currentSequences, updatedSequences, currentSequences.Length);
                cursorSequence = cursor.Cursor;

                var index = currentSequences.Length;
                foreach (var sequence in sequencesToAdd)
                {
                    sequence.Value = cursorSequence;
                    updatedSequences[index++] = sequence;
                }
            } while (!sequences.AtomicCompareExchange(updatedSequences, currentSequences));

            cursorSequence = cursor.Cursor;
            foreach (var sequence in sequencesToAdd)
            {
                sequence.Value = cursorSequence;
            }
        }

        public static bool RemoveSequence(ref Volatile.Reference<Sequence[]> sequences, Sequence sequence)
        {
            int numToRemove;
            Sequence[] oldSequences;
            Sequence[] newSequences;

            do
            {
                oldSequences = sequences.ReadFullFence();

                numToRemove = CountMatching(oldSequences, sequence);

                if (numToRemove == 0)
                    break;

                var oldSize = oldSequences.Length;
                newSequences = new Sequence[oldSize - numToRemove];

                for (int i = 0, pos = 0; i < oldSize; i++)
                {
                    var testSequence = oldSequences[i];
                    if (sequence != testSequence)
                    {
                        newSequences[pos++] = testSequence;
                    }
                }
            }
            while (!sequences.AtomicCompareExchange(newSequences, oldSequences));

            return numToRemove != 0;
        }

        private static int CountMatching<T>(T[] values, T toMatch)
        {
            var numToRemove = 0;
            foreach (var value in values)
            {
                if (ReferenceEquals(value, toMatch)) // Specifically uses identity
                {
                    numToRemove++;
                }
            }
            return numToRemove;
        }
    }
}