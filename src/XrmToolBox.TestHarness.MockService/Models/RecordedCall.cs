using System;

namespace XrmToolBox.TestHarness.MockService.Models
{
    public class RecordedCall
    {
        public int SequenceNumber { get; set; }
        public DateTime Timestamp { get; set; }
        public string Operation { get; set; }
        public string RequestTypeName { get; set; }
        public string EntityName { get; set; }
        public bool WasMatched { get; set; }
        public string MatchedDescription { get; set; }
    }
}
