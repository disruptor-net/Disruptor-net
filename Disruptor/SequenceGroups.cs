using System;
using System.Threading;

namespace Disruptor
{
    public static class SequenceGroups
    {
        public static void AddSequences(Volatile.Reference<Sequence[]> sequences, ICursored cursor, params Sequence[] sequencesToAdd) 
        {
            long cursorSequence;
            Sequence[] updatedSequences;
            Sequence[] currentSequences;

            do
            {
                currentSequences = sequences.ReadFullFence();
                updatedSequences = new Sequence[currentSequences.Length + sequencesToAdd.Length];
                Array.Copy(currentSequences, updatedSequences, currentSequences.Length + sequencesToAdd.Length);
                cursorSequence = cursor.GetCursor();

                int index = currentSequences.Length;
                foreach (Sequence sequence in sequencesToAdd)
                {
                    sequence.Value = cursorSequence;
                    updatedSequences[index++] = sequence;
                }
            } while (!sequences.AtomicCompareExchange(updatedSequences, currentSequences));

            cursorSequence = cursor.GetCursor();
            foreach (Sequence sequence in sequencesToAdd)
            {
                sequence.Value = cursorSequence;
            }
        }

        public static bool RemoveSequence(Volatile.Reference<Sequence[]> sequences, Sequence sequence)
        {
            int numToRemove;
            Sequence[] oldSequences;
            Sequence[] newSequences;

            do
            {
                oldSequences = sequences.ReadFullFence();

                numToRemove = CountMatching(oldSequences, sequence);

                if (0 == numToRemove)
                {
                    break;
                }

                int oldSize = oldSequences.Length;
                newSequences = new Sequence[oldSize - numToRemove];

                for (int i = 0, pos = 0; i < oldSize; i++)
                {
                    Sequence testSequence = oldSequences[i];
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
            int numToRemove = 0;
            foreach (T value in values)
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