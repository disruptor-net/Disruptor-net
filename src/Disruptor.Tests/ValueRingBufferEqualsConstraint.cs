using System.Text;
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
            var ringBuffer = (IValueDataProvider<long>)actual;
            for (var i = 0; i < _values.Length; i++)
            {
                valid &= ringBuffer[i].Equals(_values[i]);
            }

            return new Result(this, actual, valid);
        }

        public static ValueRingBufferEqualsConstraint IsValueRingBufferWithEvents(params long[] values)
        {
            return new ValueRingBufferEqualsConstraint(values);
        }

        public override string Description => GetStringRepresentation();

        public override string DisplayName => "Y";

        protected override string GetStringRepresentation()
        {
            return "ValueRingBuffer: " + string.Join(", ", _values);
        }

        private class Result : ConstraintResult
        {
            private readonly ValueRingBufferEqualsConstraint _constraint;

            public Result(ValueRingBufferEqualsConstraint constraint, object actualValue, bool isSuccess)
                : base(constraint, actualValue, isSuccess)
            {
                _constraint = constraint;
            }

            public override void WriteActualValueTo(MessageWriter writer)
            {
                var ringBuffer = (ValueRingBuffer<long>)ActualValue;
                var text = new StringBuilder("ValueRingBuffer: ");
                if (_constraint._values.Length != 0)
                {
                    for (var i = 0; i < _constraint._values.Length; i++)
                    {
                        text.AppendFormat("{0}, ", ringBuffer[i]);
                    }
                }

                writer.WriteActualValue(text);
            }
        }
    }
}
