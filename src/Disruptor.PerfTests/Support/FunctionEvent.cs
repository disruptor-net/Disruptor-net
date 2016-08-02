using System;

namespace Disruptor.PerfTests.Support
{
    public class FunctionEvent
    {
        public static readonly Func<FunctionEvent> EventFactory = () => new FunctionEvent();

        public long OperandOne { get; set; }
        public long OperandTwo { get; set; }
        public long StepOneResult { get; set; }
        public long StepTwoResult { get; set; }
    }
}