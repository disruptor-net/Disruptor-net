# .NET port of LMAX Disruptor

This project aims to provide the full functionality of the Disruptor to CLR projects.

## What's new?

31/12/2011 (v1.1.0)
  * Disruptor now has a [NuGet package]
  * All features available in Java Disruptor **2.7.1** have been ported 
  * Set processsor affinity with a new TaskScheduler

## Getting Started

The quickest way to get started with the disruptor is by using the [NuGet package]

## Build from source and run tests

You may also build disruptor directly from the source.
 * you need Visual Studio 2010
 * run build.bat, it will compile, run the tests and output binaries and results into Target folder

You can then run the performance tests: just launch runPerfTest.bat

[NuGet package]: http://nuget.org/packages/Disruptor