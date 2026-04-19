using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Rocket.Core.Logging.Logger;

namespace Rocket.Core.Utils
{
    public class TaskDispatcher : MonoBehaviour
    {
        private static volatile bool awake;

        // ─────────────────────────────────────────────
        // Immediate queue (lock-free)
        // ─────────────────────────────────────────────
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

        // ─────────────────────────────────────────────
        // Delayed queue (min-heap)
        // NOTE: uses Stopwatch time (thread-safe)
        // ─────────────────────────────────────────────
        private static readonly List<DelayedQueueItem> _heap = new List<DelayedQueueItem>(64);
        // private static readonly object _heapLock = new object();

        private static readonly System.Diagnostics.Stopwatch _watch = System.Diagnostics.Stopwatch.StartNew();

        public struct DelayedQueueItem
        {
            public float time; // Remains float for compatibility
            public Action action;
        }

        // ─────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────

        public static void QueueOnMainThread(Action action)
            => QueueOnMainThread(action, 0f);

        public static void QueueOnMainThread(Action action, float delay)
        {
            if (action == null)
                return;

            if (delay <= 0f)
            {
                _queue.Enqueue(action);
                return;
            }

            // THREAD-SAFE TIME (no Unity API)
            float execTime = (float) _watch.Elapsed.TotalSeconds + delay;

            lock (_heap)
            {
                HeapPush(new DelayedQueueItem { time = execTime, action = action });
            }
        }

        // ─────────────────────────────────────────────
        // Async execution
        // ─────────────────────────────────────────────

        private static int numThreads;

        public static Thread RunAsync(Action a)
        {
            while (Volatile.Read(ref numThreads) >= 8)
                Thread.Sleep(1);

            Interlocked.Increment(ref numThreads);
            ThreadPool.QueueUserWorkItem(RunAction, a);
            return null;
        }

        private static void RunAction(object action)
        {
            try { ((Action)action)(); }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Error while running action");
            }
            finally
            {
                Interlocked.Decrement(ref numThreads);
            }
        }

        private static readonly SemaphoreSlim _limit = new SemaphoreSlim(8);

        public static Task OffThread(Action a)
        {
            return Task.Run(async () =>
            {
                await _limit.WaitAsync().ConfigureAwait(false);
                try { a(); }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error while running action");
                }
                finally
                {
                    _limit.Release();
                }
            });
        }

        // ─────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────

        private void Awake()
        {
            awake = true;
        }

        private void FixedUpdate()
        {
            if (!awake)
                return;

            // ── Immediate queue (bounded execution) ──
            const int maxPerFrame = 1000; // prevents frame stalls
            int count = 0;

            while (count < maxPerFrame && _queue.TryDequeue(out var action))
            {
                try { action(); }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error executing action");
                }
                count++;
            }

            // ── Delayed queue ──
            double now = _watch.Elapsed.TotalSeconds;

            while (true)
            {
                DelayedQueueItem item;

                lock (_heap)
                {
                    if (_heap.Count == 0 || _heap[0].time > now)
                        break;

                    item = HeapPop();
                }

                try { item.action(); }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "Error executing delayed action");
                }
            }
        }

        // ─────────────────────────────────────────────
        // Min-heap
        // ─────────────────────────────────────────────

        private static void HeapPush(DelayedQueueItem item)
        {
            int i = _heap.Count;
            _heap.Add(item);

            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_heap[parent].time <= item.time)
                    break;

                _heap[i] = _heap[parent];
                i = parent;
            }

            _heap[i] = item;
        }

        private static DelayedQueueItem HeapPop()
        {
            int last = _heap.Count - 1;
            var root = _heap[0];
            var x = _heap[last];
            _heap.RemoveAt(last);

            if (_heap.Count == 0)
                return root;

            int i = 0;

            while (true)
            {
                int left = (i << 1) + 1;
                if (left >= _heap.Count)
                    break;

                int right = left + 1;
                int smallest = (right < _heap.Count && _heap[right].time < _heap[left].time)
                    ? right
                    : left;

                if (_heap[smallest].time >= x.time)
                    break;

                _heap[i] = _heap[smallest];
                i = smallest;
            }

            _heap[i] = x;
            return root;
        }
    }
}