using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Disruptor.Tests.Support;

public class DelayedTaskScheduler : TaskScheduler
{
    private readonly List<Task> _pendingTasks = new();
    private readonly List<Task> _scheduledTasks = new();

    public IEnumerable<Task> GetPendingTasks()
    {
        lock (_pendingTasks)
        {
            return _pendingTasks.ToList();
        }
    }

    protected override IEnumerable<Task> GetScheduledTasks()
    {
        lock (_scheduledTasks)
        {
            return _scheduledTasks.ToList();
        }
    }

    protected override void QueueTask(Task task)
    {
        lock (_pendingTasks)
        {
            _pendingTasks.Add(task);
        }
    }

    public void StartPendingTasks()
    {
        List<Task> tasks;
        lock (_pendingTasks)
        {
            tasks = _pendingTasks.ToList();
            _pendingTasks.Clear();
        }

        foreach (var task in tasks)
        {
            lock (_scheduledTasks)
            {
                _scheduledTasks.Add(task);
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    TryExecuteTask(task);
                }
                finally
                {
                    lock (_scheduledTasks)
                    {
                        _scheduledTasks.Remove(task);
                    }
                }
            });
        }
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
    {
        if (taskWasPreviouslyQueued)
        {
            lock (_pendingTasks)
            {
                if (!_pendingTasks.Remove(task))
                    return false;
            }
        }

        return TryExecuteTask(task);
    }
}
