using System;
using System.Threading;

namespace Disruptor;

/// <summary>
/// Provides static methods for managing groups of <see cref="ISequence"/>.
/// </summary>
internal static class SequenceGroups
{
    public static void AddSequences(ref ISequence[] sequences, ICursored cursor, params ISequence[] sequencesToAdd)
    {
        long cursorSequence;
        ISequence[] updatedSequences;
        ISequence[] currentSequences;

        do
        {
            currentSequences = Volatile.Read(ref sequences);
            updatedSequences = new ISequence[currentSequences.Length + sequencesToAdd.Length];
            Array.Copy(currentSequences, updatedSequences, currentSequences.Length);
            cursorSequence = cursor.Cursor;

            var index = currentSequences.Length;
            foreach (var sequence in sequencesToAdd)
            {
                sequence.SetValue(cursorSequence);
                updatedSequences[index++] = sequence;
            }
        }
        while (Interlocked.CompareExchange(ref sequences, updatedSequences, currentSequences) != currentSequences);

        cursorSequence = cursor.Cursor;
        foreach (var sequence in sequencesToAdd)
        {
            sequence.SetValue(cursorSequence);
        }
    }

    public static bool RemoveSequence(ref ISequence[] sequences, ISequence sequence)
    {
        int numToRemove;
        ISequence[] oldSequences;
        ISequence[] newSequences;

        do
        {
            oldSequences = Volatile.Read(ref sequences);

            numToRemove = CountMatching(oldSequences, sequence);

            if (numToRemove == 0)
                break;

            var oldSize = oldSequences.Length;
            newSequences = new ISequence[oldSize - numToRemove];

            for (int i = 0, pos = 0; i < oldSize; i++)
            {
                var testSequence = oldSequences[i];
                if (sequence != testSequence)
                {
                    newSequences[pos++] = testSequence;
                }
            }
        }
        while (Interlocked.CompareExchange(ref sequences, newSequences, oldSequences) != oldSequences);

        return numToRemove != 0;
    }

    private static int CountMatching(ISequence[] values, ISequence toMatch)
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
