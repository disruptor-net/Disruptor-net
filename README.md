# .NET port of LMAX Disruptor

This project aims to provide the full functionality of the Disruptor to CLR projects.

## What's new?

31/12/2011 (v1.1.0):

* Disruptor now has a [NuGet package]
* All features available in Java Disruptor **2.7.1** have been ported 
* Set processsor affinity with a new TaskScheduler

## Getting Started

The quickest way to get started with the disruptor is by using the [NuGet package]

## Build from source and run tests

You may also build disruptor directly from the source:
* you need Visual Studio 2010
* run build.bat, it will compile, run the tests and output binaries and results into Target folder

You can then run the performance tests: runPerfTest.bat

[NuGet package]: http://nuget.org/packages/Disruptor

## License

Copyright Olivier Deheurles

Licensed under the Apache License, Version 2.0 (the "License"); you may not use this project except in compliance with the License.

You may obtain a copy of the License at

http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
