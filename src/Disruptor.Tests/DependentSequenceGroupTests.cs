using System;
using System.Linq;
using NUnit.Framework;

namespace Disruptor.Tests;

[TestFixture]
public class DependentSequenceGroupTests
{
    [Test]
    public void ShouldGetCursorValue()
    {
        var cursor = new Sequence(42);
        var dependentSequences = new DependentSequenceGroup(cursor);

        var cursorValue = dependentSequences.CursorValue;

        Assert.AreEqual(42, cursorValue);
    }

    [Test]
    public void ShouldGetCursorValueAsValueWhenThereAreNoDependentSequences()
    {
        var cursor = new Sequence(42);
        var dependentSequences = new DependentSequenceGroup(cursor);

        var value = dependentSequences.Value;

        Assert.AreEqual(42, value);
    }

    [Test, Repeat(10)]
    public void ShouldGetMinimumValueFromSequences([Range(1, 4)] int dependentSequenceCount)
    {
        var cursor = new Sequence();
        var random = new Random();
        var sequences = Enumerable.Range(0, dependentSequenceCount)
                                  .Select(_ => new Sequence(random.Next(1000)))
                                  .Cast<ISequence>()
                                  .ToArray();

        var dependentSequences = new DependentSequenceGroup(cursor, sequences);

        var value = dependentSequences.Value;

        var expectedValue = sequences.Select(x => x.Value).Min();
        Assert.AreEqual(expectedValue, value);
    }

    [Test, Repeat(10)]
    public void ShouldGetMinimumValueFromCustomSequences([Range(1, 4)] int dependentSequenceCount)
    {
        var cursor = new Sequence();
        var random = new Random();
        var sequences = Enumerable.Range(0, dependentSequenceCount)
                                  .Select(_ => new ConstantSequence(random.Next(1000)))
                                  .Cast<ISequence>()
                                  .ToArray();

        var dependentSequences = new DependentSequenceGroup(cursor, sequences);

        var value = dependentSequences.Value;

        var expectedValue = sequences.Select(x => x.Value).Min();
        Assert.AreEqual(expectedValue, value);
    }

    private class ConstantSequence : ISequence
    {
        public ConstantSequence(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public void SetValue(long value)
        {
            throw new NotSupportedException();
        }
    }
}
