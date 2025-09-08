# Disruptor-net

The Disruptor is a high performance inter-thread message passing framework. This project is the .NET port of [LMAX Disruptor](https://github.com/LMAX-Exchange/disruptor).

The Disruptor can be succinctly defined as a circular queue with a configurable sequence of consumers. The key features are:
- Zero memory allocation after initial setup (the events are pre-allocated).
- Push-based [consumers](https://github.com/disruptor-net/Disruptor-net/wiki/Event-Handlers).
- Optionally lock-free.
- Configurable [wait strategies](https://github.com/disruptor-net/Disruptor-net/wiki/Wait-Strategies).

Since version 7, the Disruptor also supports inter-process communication using [IpcDisruptor](https://github.com/disruptor-net/Disruptor-net/wiki/IpcDisruptor).

Go to the wiki for a [more detailed introduction](https://github.com/disruptor-net/Disruptor-net/wiki/Getting-Started).