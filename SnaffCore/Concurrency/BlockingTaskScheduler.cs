using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SnaffCore.Concurrency
{
    public class BlockingStaticTaskScheduler
    {
        private readonly ConcurrentQueue<Task> _workQueue = new ConcurrentQueue<Task>();
        private readonly ConcurrentDictionary<int, Task> _runningTasks = new ConcurrentDictionary<int, Task>();
        private readonly int _maxParallel;
        private readonly int _maxQueue;
        private long _completedTasks = 0;

        public int WorkQueueCount => _workQueue.Count;
        public int RunningTaskCount => _runningTasks.Count;
        public long CompletedTaskCount => _completedTasks;
        public bool IsRunning() => WorkQueueCount > 0 || RunningTaskCount > 0;

        public BlockingStaticTaskScheduler(int maxParallel, int maxQueue = 0)
        {
            _maxParallel = maxParallel;
            _maxQueue = maxQueue;
        }

        public void New(Action action)
        {
            if (_maxQueue > 0)
            {
                while (_workQueue.Count >= _maxQueue)
                {
                    Thread.Sleep(500);
                }
            }

            Task task = new Task(action);
            _workQueue.Enqueue(task);
            TryStartTasks();
        }

        private void TryStartTasks()
        {
            while (_runningTasks.Count < _maxParallel && _workQueue.TryDequeue(out Task task))
            {
                _runningTasks.TryAdd(task.Id, task);
                task.ContinueWith(t =>
                {
                    _runningTasks.TryRemove(t.Id, out _);
                    Interlocked.Increment(ref _completedTasks);
                    TryStartTasks();
                });
                task.Start();
            }
        }
    }
}
