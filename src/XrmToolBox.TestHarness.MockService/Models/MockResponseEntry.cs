using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace XrmToolBox.TestHarness.MockService.Models
{
    public class MockResponseEntry
    {
        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("match")]
        public Dictionary<string, string> Match { get; set; } = new Dictionary<string, string>();

        [JsonProperty("response")]
        public JObject Response { get; set; }

        [JsonProperty("resultsFile")]
        public string ResultsFile { get; set; }

        [JsonProperty("delay")]
        public int? Delay { get; set; }

        [JsonProperty("fault")]
        public MockFault Fault { get; set; }
    }

    public class MockFault
    {
        [JsonProperty("errorCode")]
        public int ErrorCode { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
