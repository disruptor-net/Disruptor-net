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
    public void ShouldGetMinimumValueFromSequences([Range(1, 4)] int dependencyCount)
    {
        var cursor = new Sequence();
        var random = new Random();
        var sequences = Enumerable.Range(0, dependencyCount)
                                  .Select(_ => new Sequence(random.Next(1000)))
                                  .ToArray();

        var dependentSequences = new DependentSequenceGroup(cursor, sequences);

        var value = dependentSequences.Value;

        var expectedValue = sequences.Select(x => x.Value).Min();
        Assert.AreEqual(expectedValue, value);
    }

    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(5, 5)]
    public void ShouldGetDependentSequenceCount(int dependencyCount, int expectedDependentSequenceCount)
    {
        var cursor = new Sequence();
        var sequences = Enumerable.Range(0, dependencyCount)
                                  .Select(_ => new Sequence())
                                  .ToArray();

        var dependentSequences = new DependentSequenceGroup(cursor, sequences);

        Assert.AreEqual(expectedDependentSequenceCount, dependentSequences.DependentSequenceCount);
    }
}
