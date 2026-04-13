using Rocket.Core.RCON;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Rocket.Core.Logging
{
    public sealed class AsyncLoggerQueue
    {
        public static readonly AsyncLoggerQueue Current = new AsyncLoggerQueue();

        // lock-free queue (major win vs lock + Queue)
        private readonly ConcurrentQueue<LogEntry> _queue = new ConcurrentQueue<LogEntry>();

        // single worker state
        private int _workerRunning;

        private AsyncLoggerQueue() { }

        public void Enqueue(LogEntry le)
        {
            if (le == null) return;

            _queue.Enqueue(le);

            // fast CAS gate: ensures only one worker starts
            if (Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
            {
                ThreadPool.QueueUserWorkItem(_ => ProcessLoop());
            }
        }

        private void ProcessLoop()
        {
            try
            {
                while (true)
                {
                    if (!_queue.TryDequeue(out var le))
                    {
                        // no more work
                        return;
                    }

                    ProcessLog(le);
                }
            }
            finally
            {
                // release worker flag
                Interlocked.Exchange(ref _workerRunning, 0);

                // race condition safety: if something was enqueued after exit, restart worker
                if (!_queue.IsEmpty &&
                    Interlocked.CompareExchange(ref _workerRunning, 1, 0) == 0)
                {
                    ThreadPool.QueueUserWorkItem(_ => ProcessLoop());
                }
            }
        }

        private static readonly string logPath =
            Path.Combine(Environment.LogsDirectory, Environment.LogFile);

        private static readonly object fileLock = new object();

        private void ProcessLog(LogEntry entry)
        {
            try
            {
                // single append stream (avoids File.AppendText per log entry)
                lock (fileLock)
                {
                    using (var sw = new StreamWriter(logPath, append: true))
                    {
                        sw.Write('[');
                        sw.Write(DateTime.Now.ToString("O"));
                        sw.Write("] [");
                        sw.Write(entry.Severity.ToString());
                        sw.Write("] ");
                        sw.WriteLine(entry.Message);
                    }
                }

                if (entry.RCON && R.Settings?.Instance?.RCON?.Enabled == true)
                {
                    RCONServer.Broadcast(entry.Message);
                }
            }
            catch
            {
                // intentionally swallow logging failures to avoid recursion
            }
        }
    }
}