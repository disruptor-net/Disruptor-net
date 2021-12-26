using NUnit.Framework.Constraints;

namespace Disruptor.Tests
{
    public class RingBufferEqualsConstraint : Constraint
    {
        private readonly object?[] _values;

        public RingBufferEqualsConstraint(params object?[] values)
        {
            _values = values;
        }

        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            var valid = true;
            var ringBuffer = (RingBuffer<object[]>)(object)actual;
            for (var i = 0; i < _values.Length; i++)
            {
                var value = _values[i];
                valid &= value == null || value.Equals(ringBuffer[i][0]);
            }

            return new ConstraintResult(this, actual, valid);
        }

        public static RingBufferEqualsConstraint IsRingBufferWithEvents(params object?[] values)
        {
            return new RingBufferEqualsConstraint(values);
        }
    }
}
