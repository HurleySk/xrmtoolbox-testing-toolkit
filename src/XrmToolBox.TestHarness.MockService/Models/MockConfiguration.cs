using System.Collections.Generic;
using Newtonsoft.Json;

namespace XrmToolBox.TestHarness.MockService.Models
{
    public class MockConfiguration
    {
        [JsonProperty("settings")]
        public MockSettings Settings { get; set; } = new MockSettings();

        [JsonProperty("responses")]
        public List<MockResponseEntry> Responses { get; set; } = new List<MockResponseEntry>();
    }

    public class MockSettings
    {
        [JsonProperty("throwIfUnmatched")]
        public bool ThrowIfUnmatched { get; set; }

        [JsonProperty("defaultDelay")]
        public int DefaultDelay { get; set; }
    }
}
