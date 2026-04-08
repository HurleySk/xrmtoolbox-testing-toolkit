using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using XrmToolBox.TestHarness.MockService.Models;

namespace XrmToolBox.TestHarness.MockService
{
    public class RequestRecorder : IDisposable
    {
        private readonly ConcurrentBag<RecordedCall> _calls = new ConcurrentBag<RecordedCall>();
        private readonly object _flushLock = new object();
        private int _sequence;
        private Timer _autoFlushTimer;
        private string _autoFlushPath;
        private bool _disposed;

        public IReadOnlyList<RecordedCall> Calls =>
            _calls.OrderBy(c => c.SequenceNumber).ToList();

        public void Record(string operation, string entityName, string requestTypeName,
            bool wasMatched, string matchedDescription = null)
        {
            _calls.Add(new RecordedCall
            {
                SequenceNumber = Interlocked.Increment(ref _sequence),
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                EntityName = entityName,
                RequestTypeName = requestTypeName,
                WasMatched = wasMatched,
                MatchedDescription = matchedDescription
            });
        }

        public void StartAutoFlush(string path, int intervalMs = 2000)
        {
            _autoFlushPath = path;
            _autoFlushTimer = new Timer(_ =>
            {
                if (_disposed) return;
                try { SaveToFile(_autoFlushPath); }
                catch { /* best-effort flush */ }
            }, null, intervalMs, intervalMs);
        }

        public void SaveToFile(string path)
        {
            if (_calls.IsEmpty) return;
            lock (_flushLock)
            {
                var json = JsonConvert.SerializeObject(Calls, Formatting.Indented);
                File.WriteAllText(path, json);
            }
        }

        public void Clear()
        {
            while (_calls.TryTake(out _)) { }
            Interlocked.Exchange(ref _sequence, 0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _autoFlushTimer?.Dispose();
        }
    }
}
