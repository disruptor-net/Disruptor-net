using System;
using System.Collections.Generic;
using System.Threading;
using Disruptor.Dsl;
using Disruptor.Tests.Dsl.Stubs;
using Disruptor.Tests.Support;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl
{
    [TestFixture]
    public class UnsafeDisruptorTests
    {
        private UnsafeDisruptor<TestValueEvent> _disruptor;
        private StubExecutor _executor;
        private UnsafeRingBufferMemory _memory;

        [SetUp]
        public void SetUp()
        {
            _executor = new StubExecutor();
            _memory = UnsafeRingBufferMemory.Allocate(4, TestValueEvent.Size);
            _disruptor = new UnsafeDisruptor<TestValueEvent>(_memory.PointerToFirstEvent, _memory.EventSize, _memory.EventCount, _executor);
        }

        [TearDown]
        public void TearDown()
        {
            _disruptor.Halt();
            _executor.JoinAllThreads();
            _memory.Dispose();
        }

        [Test]
        public void ShouldPublishAndHandleEvent()
        {
            var eventCounter = new CountdownEvent(2);
            var values = new List<int>();

            _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                      .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

            _disruptor.Start();

            using (var scope = _disruptor.PublishEvent())
            {
                scope.Event().Value = 101;
            }
            using (var scope = _disruptor.PublishEvent())
            {
                scope.Event().Value = 102;
            }

            Assert.IsTrue(eventCounter.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(new List<int> { 101, 102 }, values);
        }

        [Test]
        public void ShouldPublishAndHandleEvents()
        {
            var eventCounter = new CountdownEvent(4);
            var values = new List<int>();

            _disruptor.HandleEventsWith(new TestValueEventHandler<TestValueEvent>(e => values.Add(e.Value)))
                      .Then(new TestValueEventHandler<TestValueEvent>(e => eventCounter.Signal()));

            _disruptor.Start();

            using (var scope = _disruptor.PublishEvents(2))
            {
                scope.Event(0).Value = 101;
                scope.Event(1).Value = 102;
            }
            using (var scope = _disruptor.PublishEvents(2))
            {
                scope.Event(0).Value = 103;
                scope.Event(1).Value = 104;
            }

            Assert.IsTrue(eventCounter.Wait(TimeSpan.FromSeconds(5)));
            Assert.AreEqual(new List<int> { 101, 102, 103, 104 }, values);
        }
    }
}
