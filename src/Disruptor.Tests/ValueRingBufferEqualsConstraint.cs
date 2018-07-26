using NUnit.Framework.Constraints;

namespace Disruptor.Tests
{
    public class ValueRingBufferEqualsConstraint : Constraint
    {
        private readonly long[] _values;

        public ValueRingBufferEqualsConstraint(params long[] values)
        {
            _values = values;
        }

        public override ConstraintResult ApplyTo<TActual>(TActual actual)
        {
            var valid = true;
            var ringBuffer = (ValueRingBuffer<long>)(object)actual;
            for (var i = 0; i < _values.Length; i++)
            {
                valid &= ringBuffer[i].Equals(_values[i]);
            }

            return new ConstraintResult(this, actual, valid);
        }

        public static ValueRingBufferEqualsConstraint IsValueRingBufferWithEvents(params long[] values)
        {
            return new ValueRingBufferEqualsConstraint(values);
        }
    }
}
