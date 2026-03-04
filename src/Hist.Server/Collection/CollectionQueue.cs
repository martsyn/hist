using System.Collections.Concurrent;

namespace Hist.Server.Collection;

/// <summary>
/// Thread-safe priority queue with dedup: one task per (symbol, dataType).
/// Higher-priority (lower int value) tasks are dequeued first.
/// </summary>
public class CollectionQueue
{
    private readonly Lock _lock = new();
    private readonly PriorityQueue<CollectionTask, int> _queue = new();
    private readonly Dictionary<(string, DataType), CollectionTask> _index = new();
    private readonly Dictionary<Guid, CollectionTask> _active = new();

    public int PendingCount { get { lock (_lock) return _index.Count; } }
    public int ActiveCount { get { lock (_lock) return _active.Count; } }

    /// <summary>
    /// Enqueue a task. If a task with the same (symbol, dataType) already exists,
    /// updates priority if the new one is higher (lower int).
    /// </summary>
    public void Enqueue(CollectionTask task)
    {
        lock (_lock)
        {
            var key = task.DedupeKey;
            if (_index.TryGetValue(key, out var existing))
            {
                if ((int)task.Priority < (int)existing.Priority)
                {
                    existing.Priority = task.Priority;
                    // PriorityQueue doesn't support update; mark old as superseded
                    // by inserting a new entry — the old one will be skipped during dequeue
                    _queue.Enqueue(task, (int)task.Priority);
                    _index[key] = task;
                }
                // else ignore lower-priority re-enqueue
                return;
            }

            _index[key] = task;
            _queue.Enqueue(task, (int)task.Priority);
        }
    }

    /// <summary>Try to dequeue the highest-priority pending task.</summary>
    public bool TryDequeue(out CollectionTask? task)
    {
        lock (_lock)
        {
            while (_queue.Count > 0)
            {
                var candidate = _queue.Dequeue();
                var key = candidate.DedupeKey;

                // Skip stale entries (replaced by a higher-priority version)
                if (!_index.TryGetValue(key, out var current) || current.Id != candidate.Id)
                    continue;

                if (current.Status == TaskStatus.Cancelled)
                {
                    _index.Remove(key);
                    continue;
                }

                _index.Remove(key);
                current.Status = TaskStatus.Active;
                _active[current.Id] = current;
                task = current;
                return true;
            }
        }
        task = null;
        return false;
    }

    public void CompleteTask(Guid id, bool success, string? error = null)
    {
        lock (_lock)
        {
            if (_active.TryGetValue(id, out var task))
            {
                task.Status = success ? TaskStatus.Completed : TaskStatus.Failed;
                task.ErrorMessage = error;
                _active.Remove(id);
            }
        }
    }

    public bool CancelPending(Guid id)
    {
        lock (_lock)
        {
            foreach (var task in _index.Values)
            {
                if (task.Id == id)
                {
                    task.Status = TaskStatus.Cancelled;
                    return true;
                }
            }
            return false;
        }
    }

    public bool UpdatePriority(Guid id, TaskPriority priority)
    {
        lock (_lock)
        {
            foreach (var task in _index.Values)
            {
                if (task.Id == id)
                {
                    task.Priority = priority;
                    _queue.Enqueue(task, (int)priority);
                    return true;
                }
            }
            return false;
        }
    }

    public List<CollectionTask> GetPendingTasks()
    {
        lock (_lock)
            return [.. _index.Values];
    }

    public List<CollectionTask> GetActiveTasks()
    {
        lock (_lock)
            return [.. _active.Values];
    }
}
