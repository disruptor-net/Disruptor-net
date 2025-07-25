﻿using NUnit.Framework;
using ObjectLayoutInspector;

namespace Disruptor.Tests;

[TestFixture, Explicit("Manual tests")]
public class ObjectLayoutTests
{
    [Test]
    public void PrintMultiProducerSequencerLayout()
    {
        TypeLayout.PrintLayout<MultiProducerSequencer>();
    }
}
