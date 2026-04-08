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
    public class RequestRecorder
    {
        private readonly ConcurrentBag<RecordedCall> _calls = new ConcurrentBag<RecordedCall>();
        private int _sequence;

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

        public void SaveToFile(string path)
        {
            var json = JsonConvert.SerializeObject(Calls, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        public void Clear()
        {
            while (_calls.TryTake(out _)) { }
            Interlocked.Exchange(ref _sequence, 0);
        }
    }
}
