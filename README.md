# Disruptor-net

[![Build](https://github.com/Disruptor-net/Disruptor-net/workflows/Build/badge.svg)](https://github.com/Disruptor-net/Disruptor-net/actions?query=workflow%3ABuild)
[![NuGet](https://buildstats.info/nuget/Disruptor)](http://www.nuget.org/packages/Disruptor/)

## Overview

The Disruptor is a high performance inter-thread message passing framework. This project is the .NET port of [LMAX Disruptor](https://github.com/LMAX-Exchange/disruptor).

The Disruptor can be succinctly defined as a circular queue with a configurable sequence of consumers. The key features are:
- Zero memory allocation after initial setup (the events are pre-allocated).
- Push-based consumers.
- Optionally lock-free.
- Configurable [wait strategies](https://github.com/disruptor-net/Disruptor-net/wiki/Wait-Strategies).

Most of the information from the Java documentation is applicable to the .NET version, especially the [core concepts](https://github.com/LMAX-Exchange/disruptor/wiki/Introduction).

The quickest way to get started with the disruptor is by using the [NuGet package](https://www.nuget.org/packages/Disruptor).

## Release notes

[Version 5.0.0 RC1](https://github.com/disruptor-net/Disruptor-net/releases/tag/5.0.0-rc1) has been released and is available on NuGet.

## Supported runtimes

- .NET 5.0+
- .NET Standard 2.1

## Basic usage

First, you need to define your event (message) type:

```cs
public class SampleEvent
{
    public int Id { get; set; }
    public double Value { get; set; }
}
```

You also need to create a consumer:

```cs
public class SampleEventHandler : IEventHandler<SampleEvent>
{
    public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
    {
        Console.WriteLine($"Event: {data.Id} => {data.Value}");
    }
}
```

Then you can setup the Disruptor:

```cs
var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), bufferSize: 1024);

disruptor.HandleEventsWith(new SampleEventHandler());

disruptor.Start();
```

Finally, you can publish events:

```cs
using (var scope = disruptor.PublishEvent())
{
    var data = scope.Event();
    data.Id = 42;
    data.Value = 1.1;
}
```

Other event [publication options](https://github.com/disruptor-net/Disruptor-net/wiki/Publication-Options) are described in the Wiki.

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
