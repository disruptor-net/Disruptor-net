# Disruptor-net

[![Build](https://github.com/Disruptor-net/Disruptor-net/actions/workflows/build.yml/badge.svg?branch=master)](https://github.com/disruptor-net/Disruptor-net/actions?query=branch%3Amaster+workflow%3ABuild++)
[![NuGet package](https://img.shields.io/nuget/v/Disruptor.svg?logo=NuGet)](https://www.nuget.org/packages/Disruptor)

## Overview

The Disruptor is a high performance inter-thread message passing framework. This project is the .NET port of [LMAX Disruptor](https://github.com/LMAX-Exchange/disruptor).

The Disruptor can be succinctly defined as a circular queue with a configurable sequence of consumers. The key features are:
- Zero memory allocation after initial setup (the events are pre-allocated).
- Push-based [consumers](https://github.com/disruptor-net/Disruptor-net/wiki/Event-Handlers).
- Optionally lock-free.
- Configurable [wait strategies](https://github.com/disruptor-net/Disruptor-net/wiki/Wait-Strategies).

Since version 7, the Disruptor also supports inter-process communication using [IpcDisruptor](https://github.com/disruptor-net/Disruptor-net/wiki/IpcDisruptor).

## Releases

- Latest stable version is `6.0.1` ([package](https://www.nuget.org/packages/Disruptor/6.0.1), [changes](https://github.com/disruptor-net/Disruptor-net/releases?q=tag%3A6.0&expanded=true)).
- Latest RC version is `7.0.0-rc1` ([package](https://www.nuget.org/packages/Disruptor/7.0.0-rc1), [changes](https://github.com/disruptor-net/Disruptor-net/releases/tag/7.0.0-rc1)).

## Supported runtimes

- .NET 6.0+
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

Then you can create and setup the Disruptor:
```cs
var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), ringBufferSize: 1024);

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

Go to the wiki for a [more detailed introduction](https://github.com/disruptor-net/Disruptor-net/wiki/Getting-Started).

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
