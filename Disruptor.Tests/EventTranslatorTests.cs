using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class EventTranslatorTests
    {
        private const string TestValue = "Wibble";


        [Test]
        public void ShouldTranslateOtherDataIntoAnEvent()
        {
            StubEvent eventData = new StubEvent(0);
            
            IEventTranslator<StubEvent> eventTranslator = new ExampleEventTranslator(TestValue);

            eventTranslator.TranslateTo(eventData, 0);

            Assert.AreEqual(TestValue, eventData.TestString);
        }

        public class ExampleEventTranslator : IEventTranslator<StubEvent>
        {
            private static string _testValue;

            public ExampleEventTranslator(string testValue)
            {
                _testValue = testValue;
            }
           
            public void TranslateTo(StubEvent eventData, long sequence)
            {
                eventData.TestString = _testValue;
            }
        }
}


}
