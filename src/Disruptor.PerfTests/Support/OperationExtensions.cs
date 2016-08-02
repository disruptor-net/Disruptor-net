using System;

namespace Disruptor.PerfTests.Support
{
    public static class OperationExtensions
    {
        public static long Op(this Operation operation, long lhs, long rhs)
        {
            switch (operation)
            {
                case Operation.Addition:
                    return lhs + rhs;
                case Operation.Subtraction:
                    return lhs - rhs;
                case Operation.And:
                    return lhs & rhs;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }
    }
}