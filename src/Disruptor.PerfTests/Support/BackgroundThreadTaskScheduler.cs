using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.PerfTests.Support;

/// <summary>
/// Runs every task on a dedicated background thread with the specified cpu affinity.
/// </summary>
public class BackgroundThreadTaskScheduler : TaskScheduler
{
    private readonly ConcurrentDictionary<Task, int> _tasks = new();
    private readonly int? _cpu;

    public BackgroundThreadTaskScheduler(int? cpu = null)
    {
        _cpu = cpu;
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        return _tasks.Keys;
    }

    protected override void QueueTask(Task task)
    {
        _tasks.TryAdd(task, 0);

        var thread = new Thread(() => Run()) { IsBackground = true };
        thread.Start();

        void Run()
        {
            using var _ = ThreadAffinityUtil.SetThreadAffinity(_cpu, ThreadPriority.Highest);
            try
            {
                TryExecuteTask(task);
            }
            finally
            {
                _tasks.TryRemove(task, out var _);
            }
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        return TryExecuteTask(task);
    }
}
