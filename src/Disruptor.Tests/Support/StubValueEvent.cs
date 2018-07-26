using System;

namespace Disruptor.Tests.Support
{
    public struct StubValueEvent : IEquatable<StubValueEvent>
    {
        public StubValueEvent(int i)
        {
            Value = i;
            TestString = null;
        }

        public int Value { get; set; }
        public string TestString { get; set; }

        public static readonly Func<StubValueEvent> EventFactory = () => new StubValueEvent(-1);

        public override int GetHashCode()
        {
            return Value;
        }

        public bool Equals(StubValueEvent other)
        {
            return other.Value == Value;
        }

        public override bool Equals(object obj)
        {
            return obj is StubValueEvent other && Equals(other);
        }

        public override string ToString()
        {
            return string.Format("Value: {0}, TestString: {1}", Value, TestString);
        }

        public void Copy(StubValueEvent evt)
        {
            Value = evt.Value;
        }

        public struct Translator : IValueEventTranslator<StubValueEvent>
        {
            private readonly int _value;
            private readonly string _testString;

            public Translator(int value, string testString)
            {
                _value = value;
                _testString = testString;
            }

            public void TranslateTo(ref StubValueEvent eventData, long sequence)
            {
                eventData.Value = _value;
                eventData.TestString = _testString;
            }
        }
    }
}
