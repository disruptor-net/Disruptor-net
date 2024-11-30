using System;
using System.Linq;
using NUnit.Framework.Constraints;

namespace Disruptor.Tests;

public static class IsValueRingBuffer
{
    public static Constraint WithEvents<T>(params T[] values)
        where T : struct, IEquatable<T>
    {
        return new HasEventsConstraint<T>(values);
    }

    private class HasEventsConstraint<T> : Constraint
        where T : struct, IEquatable<T>
    {
        private readonly T[] _values;

        public HasEventsConstraint(T[] values)
        {
            _values = values;
        }

        public override string Description => $"ValueRingBuffer with events:  {string.Join(" ", _values.Select(x => "{" + x + "}"))}";

        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            if (actual is IValueDataProvider<T> ringBuffer)
            {
                var valid = true;
                for (var i = 0; i < _values.Length; i++)
                {
                    var value = _values[i];
                    valid &= value.Equals(ringBuffer[i]);
                }

                return new ConstraintResult(this, actual, valid);
            }

            return new ConstraintResult(this, actual, false);
        }
    }
}
