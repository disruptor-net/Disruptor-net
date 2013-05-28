using System;
using System.Collections.Concurrent;
using System.Threading;
using Moq;
using NUnit.Framework;

namespace Disruptor.Tests
{
    [TestFixture]
    public class MultiThreadedClaimStrategyTests
    {
        private const int BufferSize = 8;
        private IClaimStrategy _claimStrategy;

        [SetUp]
        public void SetUp()
        {
            _claimStrategy = new MultiThreadedClaimStrategy(BufferSize);
        }
            
        [Test]
        public void ShouldNotCreateBufferWithNonPowerOf2()
        {
            Assert.Throws<ArgumentException>(() => new MultiThreadedClaimStrategy(1024, 129));
        }

        [Test]
        public void ShouldGetCorrectBufferSize()
        {
            Assert.AreEqual(BufferSize, _claimStrategy.BufferSize);
        }

        [Test]
        public void ShouldGetInitialSequence()
        {
            Assert.AreEqual(Sequencer.InitialCursorValue, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialSequence()
        {
            var dependentSequence = new Mock<Sequence>();
            
            Sequence[] dependentSequences = { dependentSequence.Object };
            const long expectedSequence = Sequencer.InitialCursorValue + 1L;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldClaimInitialBatchOfSequences()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };
            const int batchSize = 5;
            const long expectedSequence = Sequencer.InitialCursorValue + batchSize;

            Assert.AreEqual(expectedSequence, _claimStrategy.IncrementAndGet(batchSize, dependentSequences));
            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSetSequenceToValue()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };
            const int expectedSequence = 5;
            _claimStrategy.SetSequence(expectedSequence, dependentSequences);

            Assert.AreEqual(expectedSequence, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldHaveInitialAvailableCapacity()
        {
            var dependentSequence = new Mock<Sequence>();

            Sequence[] dependentSequences = { dependentSequence.Object };

            Assert.IsTrue(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotHaveAvailableCapacityWhenBufferIsFull()
        {
            var dependentSequence = new Mock<Sequence>();
            dependentSequence.Setup(ds => ds.Value).Returns(Sequencer.InitialCursorValue);
            
            Sequence[] dependentSequences = { dependentSequence.Object };

            _claimStrategy.SetSequence(_claimStrategy.BufferSize - 1L, dependentSequences);

            Assert.IsFalse(_claimStrategy.HasAvailableCapacity(1, dependentSequences));
        }

        [Test]
        public void ShouldNotReturnNextClaimSequenceUntilBufferHasReserve()
        {
            var dependentSequence = new Sequence(Sequencer.InitialCursorValue);
            Sequence[] dependentSequences = { dependentSequence };
            _claimStrategy.SetSequence(_claimStrategy.BufferSize - 1L, dependentSequences);

            var done = new Volatile.Boolean(false);
            var beforeLatch = new ManualResetEvent(false);
            var afterLatch = new ManualResetEvent(false);

            new Thread(
                ()=>
                    {
                        beforeLatch.Set();

                        Assert.AreEqual(_claimStrategy.BufferSize, _claimStrategy.IncrementAndGet(dependentSequences));

                        done.WriteFullFence(true);
                        afterLatch.Set();
                    }).Start();

            beforeLatch.WaitOne();

            Thread.Sleep(1000);
            Assert.IsFalse(done.ReadFullFence());

            dependentSequence.Value =  dependentSequence.Value + 1L;

            afterLatch.WaitOne();
            Assert.AreEqual(_claimStrategy.BufferSize, _claimStrategy.Sequence);
        }

        [Test]
        public void ShouldSerialisePublishingOnTheCursor()
        {
            var dependentSequence = new Sequence(Sequencer.InitialCursorValue);
            Sequence[] dependentSequences = { dependentSequence };

            long sequence = _claimStrategy.IncrementAndGet(dependentSequences);

            var cursor = new Sequence(Sequencer.InitialCursorValue);
            _claimStrategy.SerialisePublishing(sequence, cursor, 1);

            Assert.AreEqual(sequence, cursor.Value);
        }

        [Test]
        [ExpectedException(typeof(InsufficientCapacityException))]
        public void ShouldThrowExceptionIfCapacityIsNotAvailable()
        {
            Sequence dependentSequence = new Sequence();
            Sequence[] dependentSequences = { dependentSequence };

            _claimStrategy.CheckAndIncrement(9, 1, dependentSequences);
        }
    
        [Test]
        public void ShouldSucessfullyGetNextValueIfLessThanCapacityIsAvailable()
        {
            Sequence dependentSequence = new Sequence();
            Sequence[] dependentSequences = { dependentSequence };

            for (long i = 0; i < 8; i++)
            {
                Assert.AreEqual(i, _claimStrategy.CheckAndIncrement(1, 1, dependentSequences));
            }
        }
    
        [Test]
        public void ShouldSucessfullyGetNextValueIfLessThanCapacityIsAvailableWhenClaimingMoreThanOne()
        {
            Sequence dependentSequence = new Sequence();
            Sequence[] dependentSequences = { dependentSequence };

            Assert.AreEqual(3, _claimStrategy.CheckAndIncrement(4, 4, dependentSequences));
            Assert.AreEqual(7, _claimStrategy.CheckAndIncrement(4, 4, dependentSequences));
        }
    
        [Test]
        public void ShouldOnlyClaimWhatsAvailable()
        {
            Sequence dependentSequence = new Sequence();
            Sequence[] dependentSequences = { dependentSequence };
        
            for (int j = 0; j < 1000; j++)
            {
                int numThreads = BufferSize * 2;
                IClaimStrategy claimStrategy = new MultiThreadedClaimStrategy(BufferSize);
                Volatile.LongArray claimed = new Volatile.LongArray(numThreads);
                Barrier barrier = new Barrier(numThreads);
                Thread[] ts = new Thread[numThreads];
            
                for (int i = 0; i < numThreads; i++)
                {
                    ts[i] = new Thread(() =>
                        {
                            try
                            {
                                barrier.SignalAndWait();
                                long next = claimStrategy.CheckAndIncrement(1, 1, dependentSequences);
                                claimed.AtomicIncrementAndGet((int) next);
                            }
                            catch (Exception e)
                            {                                
                            }
                        });
                }
            
                foreach (Thread t in ts)
                {
                    t.Start();
                }
            
                foreach (Thread t in ts)
                {
                    t.Join();
                }
            
                for (int i = 0; i < BufferSize; i++)
                {
                    Assert.AreEqual(1L, claimed.ReadFullFence(i), "j = " + j + ", i = " + i);
                }
            
                for (int i = BufferSize; i < numThreads; i++)
                {
                    Assert.AreEqual(0L, claimed.ReadFullFence(i), "j = " + j + ", i = " + i);
                }
            }
        }

        [Test]
        public void ShouldSerialisePublishingOnTheCursorWhenTwoThreadsArePublishing()
        {
            var dependentSequence = new Sequence(Sequencer.InitialCursorValue);
            var dependentSequences = new[] { dependentSequence };

            var threadSequences = new ConcurrentDictionary<long, string>();
            var cursor = new SequenceStub(Sequencer.InitialCursorValue, threadSequences);

            var mre = new ManualResetEvent(false);

            var t1 = new Thread(
                () =>
                {
                    var sequence = _claimStrategy.IncrementAndGet(dependentSequences);
                    mre.Set();

                    Thread.Sleep(1000);

                    _claimStrategy.SerialisePublishing(sequence, cursor, 1);
                });

            var t2 = new Thread(
                () =>
                {
                    mre.WaitOne();
                    var sequence = _claimStrategy.IncrementAndGet(dependentSequences);

                    _claimStrategy.SerialisePublishing(sequence, cursor, 1);
                });

            t1.Name = "tOne";
            t2.Name = "tTwo";
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();

            Assert.IsNotNull(threadSequences[0]);
            Assert.IsNotNull(threadSequences[1]);
        }

        public class SequenceStub : Sequence
        {
            private readonly ConcurrentDictionary<long, string> _threadSequences = new ConcurrentDictionary<long, string>();

            public SequenceStub(long initialValue, ConcurrentDictionary<long, string> threadSequences)
                : base(initialValue)
            {
                _threadSequences = threadSequences;
            }

            public override bool  CompareAndSet(long expectedSequence, long nextSequence)
            {
                var threadName = Thread.CurrentThread.Name;
                if ("tOne" == threadName || "tTwo" == threadName)
                {
                    _threadSequences[nextSequence] = threadName;
                }
 	            return base.CompareAndSet(expectedSequence, nextSequence);
            }
        }

        //[Test]
        //public void ShouldSerialisePublishingOnTheCursorWhenTwoThreadsArePublishing()
        //{
        //    Sequence dependentSequence = new Sequence(Sequencer.InitialCursorValue);
        //    Sequence[] dependentSequences = { dependentSequence };

        //    AtomicReferenceArray<String> threadSequences = new AtomicReferenceArray<String>(2);

        //    Sequence cursor = new Sequence(Sequencer.InitialCursorValue)
        //    {
        //        @Override
        //        public boolean compareAndSet(long expectedSequence, long nextSequence)
        //        {
        //            String threadName = Thread.currentThread().getName();
        //            if ("tOne".equals(threadName) || "tTwo".equals(threadName))
        //            {
        //                threadSequences.set((int)nextSequence, threadName);
        //            }
                
        //            return super.compareAndSet(expectedSequence, nextSequence);
        //        }
        //    };

        //    CountDownLatch orderingLatch = new CountDownLatch(1);

        //    Runnable publisherOne = new Runnable()
        //    {
        //        @Override
        //        public void run()
        //        {
        //            long sequence = _claimStrategy.IncrementAndGet(dependentSequences);
        //            orderingLatch.countDown();

        //            try
        //            {
        //                Thread.sleep(1000L);
        //            }
        //            catch (InterruptedException e)
        //            {
        //                // don't care
        //            }

        //            _claimStrategy.SerialisePublishing(sequence, cursor, 1);
        //        }
        //    };

        //    Runnable publisherTwo = new Runnable()
        //    {
        //        @Override
        //        public void run()
        //        {
        //            try
        //            {
        //                orderingLatch.await();
        //            }
        //            catch (InterruptedException e)
        //            {
        //                e.printStackTrace();
        //            }

        //            long sequence = _claimStrategy.IncrementAndGet(dependentSequences);

        //            _claimStrategy.SerialisePublishing(sequence, cursor, 1);
        //        }
        //    };

        //    Thread tOne = new Thread(publisherOne);
        //    Thread tTwo = new Thread(publisherTwo);
        //    tOne.setName("tOne");
        //    tTwo.setName("tTwo");
        //    tOne.start();
        //    tTwo.start();
        //    tOne.join();
        //    tTwo.join();
        
        //    // One thread can end up setting both sequences.
        //    assertThat(threadSequences.get(0), is(notNullValue()));
        //    assertThat(threadSequences.get(1), is(notNullValue()));
        //}

        //@Test
        //public void shouldSerialisePublishingOnTheCursorWhenTwoThreadsArePublishingWithBatches() throws InterruptedException
        //{
        //    final Sequence[] dependentSequences = {};
        //    final Sequence cursor = new Sequence(Sequencer.INITIAL_CURSOR_VALUE);

        //    final CountDownLatch orderingLatch = new CountDownLatch(2);
        //    final int iterations = 1000000;
        //    final int batchSize = 44;

        //    final Runnable publisherOne = new Runnable()
        //    {
        //        @Override
        //        public void run()
        //        {
        //            int counter = iterations;
        //            while (-1 != --counter)
        //            {
        //                final long sequence = claimStrategy.incrementAndGet(batchSize, dependentSequences);
        //                claimStrategy.serialisePublishing(sequence, cursor, batchSize);
        //            }
                
        //            orderingLatch.countDown();
        //        }
        //    };

        //    final Runnable publisherTwo = new Runnable()
        //    {
        //        @Override
        //        public void run()
        //        {
        //            int counter = iterations;
        //            while (-1 != --counter)
        //            {
        //                final long sequence = claimStrategy.incrementAndGet(batchSize, dependentSequences);
        //                claimStrategy.serialisePublishing(sequence, cursor, batchSize);
        //            }
                
        //            orderingLatch.countDown();
        //        }
        //    };

        //    Thread tOne = new Thread(publisherOne);
        //    tOne.setDaemon(true);
        //    Thread tTwo = new Thread(publisherTwo);
        //    tTwo.setDaemon(true);
        //    tOne.setName("tOne");
        //    tTwo.setName("tTwo");
        //    tOne.start();
        //    tTwo.start();
        
        //    assertThat("Timed out waiting for threads", orderingLatch.await(10, TimeUnit.SECONDS), is(true));
        //}
    }
}