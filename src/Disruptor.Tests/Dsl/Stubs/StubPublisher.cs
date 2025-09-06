﻿using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Disruptor.Tests.Dsl.Stubs;

public class StubPublisher
{
    private readonly ISequenced _ringBuffer;
    private volatile bool _running;
    private volatile int _publicationCount;

    public StubPublisher(ISequenced ringBuffer)
    {
        _ringBuffer = ringBuffer;
    }

    public void Halt()
    {
        _running = false;
    }

    public Task Start()
    {
        _running = true;
        return Task.Factory.StartNew(RunImpl, TaskCreationOptions.LongRunning);

        void RunImpl()
        {
            while (_running)
            {
                var sequence = _ringBuffer.Next();
                _ringBuffer.Publish(sequence);
                _publicationCount++;
            }
        }
    }

    public void AssertProducerReaches(int expectedPublicationCount, bool strict)
    {
        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(500);

        while (_publicationCount < expectedPublicationCount && stopwatch.Elapsed < timeout)
        {
            Thread.Yield();
        }

        if (strict)
        {
            Assert.That(_publicationCount, Is.EqualTo(expectedPublicationCount));
        }
        else
        {
            var actualPublicationCount = _publicationCount;
            Assert.That(actualPublicationCount >= expectedPublicationCount, "Producer reached unexpected count. Expected at least " + expectedPublicationCount + " but only reached " + actualPublicationCount);
        }
    }
}
