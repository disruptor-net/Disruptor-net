using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Disruptor.Benchmarks;

public class DependentSequenceGroupBenchmarks
{
    private const int _operationsPerInvoke = 200;

    private ISequence _dependentSequence;
    private S1 _s1;
    private S2 _s2;

    [Params(0, 1, 2, 3)]
    public int DependentSequenceCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        var cursor = new Sequence(42);
        var dependentSequences = Enumerable.Range(1, DependentSequenceCount).Select(x => new Sequence(42 - x)).Cast<ISequence>().ToArray();

        _dependentSequence = CreateReadOnlySequence(cursor, dependentSequences);
        _s1 = new S1(cursor, dependentSequences);
        _s2 = new S2(cursor, dependentSequences);
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = _operationsPerInvoke)]
    public long UseInterface()
    {
        var sum = 0L;

        for (var i = 0; i < _operationsPerInvoke; i++)
        {
            sum += _dependentSequence.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public long UseS1()
    {
        var sum = 0L;

        for (var i = 0; i < _operationsPerInvoke; i++)
        {
            sum += _s1.Value;
        }

        return sum;
    }

    [Benchmark(OperationsPerInvoke = _operationsPerInvoke)]
    public long UseS2()
    {
        var sum = 0L;

        for (var i = 0; i < _operationsPerInvoke; i++)
        {
            sum += _s2.Value;
        }

        return sum;
    }

    private static ISequence CreateReadOnlySequence(Sequence cursorSequence, ISequence[] dependentSequences)
    {
        if (dependentSequences.Length == 0)
        {
            return cursorSequence;
        }

        if (dependentSequences.Length == 1)
        {
            return dependentSequences[0];
        }

        return new ReadOnlySequenceGroup(dependentSequences);
    }

    private class S1
    {
        private readonly Sequence[] _typedSequences;
        private readonly ISequence[] _untypedSequences;

        public S1(Sequence cursor, ISequence[] dependentSequences)
        {
            if (dependentSequences.Length == 0)
            {
                _typedSequences = new[] { cursor };
                _untypedSequences = Array.Empty<ISequence>();
            }
            else if (dependentSequences.All(x => x is Sequence))
            {
                _typedSequences = dependentSequences.Cast<Sequence>().ToArray();
                _untypedSequences = Array.Empty<ISequence>();

            }
            else
            {
                _typedSequences = Array.Empty<Sequence>();
                _untypedSequences = dependentSequences.ToArray();
            }
        }

        public long Value
        {
            get
            {
                if (_typedSequences.Length != 0)
                {
                    var minimum = long.MaxValue;
                    foreach (var sequence in _typedSequences)
                    {
                        var sequenceValue = sequence.Value;
                        if (sequenceValue < minimum)
                            minimum = sequenceValue;
                    }

                    return minimum;
                }

                return GetValueFromUntypedSequences();
            }
        }

        private long GetValueFromUntypedSequences()
        {
            var minimum = long.MaxValue;
            foreach (var sequence in _untypedSequences)
            {
                var sequenceValue = sequence.Value;
                if (sequenceValue < minimum)
                    minimum = sequenceValue;
            }

            return minimum;
        }
    }

    private class S2
    {
        private readonly Sequence[] _typedSequences;
        private readonly ISequence[] _untypedSequences;

        public S2(Sequence cursor, ISequence[] dependentSequences)
        {
            if (dependentSequences.Length == 0)
            {
                _typedSequences = new[] { cursor };
                _untypedSequences = Array.Empty<ISequence>();
            }
            else if (dependentSequences.All(x => x is Sequence))
            {
                _typedSequences = dependentSequences.Cast<Sequence>().ToArray();
                _untypedSequences = Array.Empty<ISequence>();

            }
            else
            {
                _typedSequences = Array.Empty<Sequence>();
                _untypedSequences = dependentSequences.ToArray();
            }
        }

        public long Value
        {
            get
            {
                if (_typedSequences.Length == 1)
                {
                    return _typedSequences[0].Value;
                }

                if (_typedSequences.Length != 0)
                {
                    var minimum = long.MaxValue;
                    foreach (var sequence in _typedSequences)
                    {
                        var sequenceValue = sequence.Value;
                        if (sequenceValue < minimum)
                            minimum = sequenceValue;
                    }

                    return minimum;
                }

                return GetValueFromUntypedSequences();
            }
        }

        private long GetValueFromUntypedSequences()
        {
            var minimum = long.MaxValue;
            foreach (var sequence in _untypedSequences)
            {
                var sequenceValue = sequence.Value;
                if (sequenceValue < minimum)
                    minimum = sequenceValue;
            }

            return minimum;
        }
    }
}
