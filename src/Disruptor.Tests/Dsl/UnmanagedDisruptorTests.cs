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
    public class UnmanagedDisruptorTests : IDisposable
    {
        private readonly UnmanagedDisruptor<TestValueEvent> _disruptor;
        private readonly StubTaskScheduler _taskScheduler;
        private readonly UnmanagedRingBufferMemory _memory;

        public UnmanagedDisruptorTests()
        {
            _taskScheduler = new StubTaskScheduler();
            _memory = UnmanagedRingBufferMemory.Allocate(4, TestValueEvent.Size);
            _disruptor = new UnmanagedDisruptor<TestValueEvent>(_memory.PointerToFirstEvent, _memory.EventSize, _memory.EventCount, _taskScheduler);
        }

        public void Dispose()
        {
            _disruptor.Halt();
            _taskScheduler.JoinAllThreads();
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
