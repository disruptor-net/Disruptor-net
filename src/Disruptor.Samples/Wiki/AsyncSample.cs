using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Disruptor.Dsl;

namespace Disruptor.Samples.Wiki
{
    public static class AsyncSample
    {
        public static void Run()
        {
            var disruptor = new Disruptor<SampleEvent>(() => new SampleEvent(), 1024);
            var handler1 = new H1(new SampleEventPersister());
            var handler2 = new H2();

            disruptor.HandleEventsWith(handler1).Then(handler2);
        }

        public class SampleEvent
        {
            public int Id { get; set; }
            public double Value { get; set; }
            public Task SaveTask { get; set; }
            public TaskResult SaveResult { get; set; }

            public override string ToString()
            {
                return $"{nameof(Id)}: {Id}, {nameof(Value)}: {Value}";
            }
        }

        public class H1 : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;

            public H1(ISampleEventPersister persister)
            {
                _persister = persister;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                var task = _persister.SaveAsync(data);
                // How to deal with the task in a void method?
            }
        }

        public class H2 : IEventHandler<SampleEvent>
        {
            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
            }
        }

        public record TaskResult(bool Succeeded, Exception Error);

        public static TaskResult WaitTaskResult(this Task task, TimeSpan timeout)
        {
            try
            {
                return task.Wait(timeout) ? new(true, null) : new(false, new TimeoutException());
            }
            catch (Exception e)
            {
                return new(false, e);
            }
        }

        public class H1_FullySync : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;
            private readonly TimeSpan _saveTimeout;

            public H1_FullySync(ISampleEventPersister persister, TimeSpan saveTimeout)
            {
                _persister = persister;
                _saveTimeout = saveTimeout;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                var task = _persister.SaveAsync(data);

                // It might be useful to store the save result so it
                // can be accessed in next event handlers.
                data.SaveResult = task.WaitTaskResult(_saveTimeout);
            }
        }

        public class H1_FullyAsync : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;

            public H1_FullyAsync(ISampleEventPersister persister)
            {
                _persister = persister;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                var task = _persister.SaveAsync(data);

                // Fire-and-forgetting the task should really be avoid.
                // If you do so, you should add least limit the number of
                // ongoing save tasks, for example using a semaphore.

                task.ContinueWith(t => OnError(t.Exception), TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.OnlyOnFaulted);

                static void OnError(Exception ex)
                {
                    // TODO: handle the task error.
                }
            }
        }

        /// <summary>
        /// H1 can start processing e2 before the task from e1 is completed.
        /// </summary>
        public class H1_WaitInNextHandler : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;

            public H1_WaitInNextHandler(ISampleEventPersister persister)
            {
                _persister = persister;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                data.SaveTask = _persister.SaveAsync(data);
            }
        }

        public class H2_WaitInNextHandler : IEventHandler<SampleEvent>
        {
            private readonly TimeSpan _saveTimeout;

            public H2_WaitInNextHandler(TimeSpan saveTimeout)
            {
                _saveTimeout = saveTimeout;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                var saveResult = data.SaveTask.WaitTaskResult(_saveTimeout);
                // TODO: handle the task error or save the result in the event.
                // TODO: add H2 own logic.
            }
        }

        /// <summary>
        /// The handler 2 can start processing e1 before the task from e1 is completed.
        /// </summary>
        public class H1_WaitInCurrentHandler : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;
            private readonly TimeSpan _saveTimeout;
            private Task _previousSaveTask = Task.CompletedTask;

            public H1_WaitInCurrentHandler(ISampleEventPersister persister, TimeSpan saveTimeout)
            {
                _persister = persister;
                _saveTimeout = saveTimeout;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                var saveResult = _previousSaveTask.WaitTaskResult(_saveTimeout);
                // TODO: handle the task error.

                _previousSaveTask = _persister.SaveAsync(data);
            }
        }

        public class H1_Batching1 : IBatchEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;

            public H1_Batching1(ISampleEventPersister persister)
            {
                _persister = persister;
            }

            public void OnBatch(ReadOnlySpan<SampleEvent> batch, long sequence)
            {
                var task = _persister.SaveAsync(batch.ToArray());
                // Do something with the task depending on you design option
            }
        }

        public class H1_Batching2 : IEventHandler<SampleEvent>
        {
            private readonly ISampleEventPersister _persister;
            private readonly List<Task> _pendingTasks = new List<Task>();

            public H1_Batching2(ISampleEventPersister persister)
            {
                _persister = persister;
            }

            public void OnEvent(SampleEvent data, long sequence, bool endOfBatch)
            {
                _pendingTasks.Add(_persister.SaveAsync(data));

                if (endOfBatch)
                    Flush();

                void Flush()
                {
                    _pendingTasks.Add(_persister.FlushAsync());

                    var task = Task.WhenAll(_pendingTasks);
                    // Do something with the task depending on you design option
                }
            }
        }

        public interface ISampleEventPersister
        {
            void Save(SampleEvent e);
            Task FlushAsync();
            Task SaveAsync(SampleEvent e);
            Task SaveAsync(IEnumerable<SampleEvent> events);
        }

        private class SampleEventPersister : ISampleEventPersister
        {
            public void Save(SampleEvent e)
            {
                Thread.Sleep(200);
                Console.WriteLine($"Saved: {e}");
            }

            public async Task FlushAsync()
            {
                await Task.Delay(100);
                Console.WriteLine($"Flushed");
            }

            public async Task SaveAsync(SampleEvent e)
            {
                await Task.Delay(200);
                Console.WriteLine($"Saved: {e}");
            }

            public async Task SaveAsync(IEnumerable<SampleEvent> events)
            {
                await Task.Delay(200);
                Console.WriteLine($"Saved: {events.Count()} events");
            }
        }
    }
}
