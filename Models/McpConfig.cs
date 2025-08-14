using System;
using Newtonsoft.Json;

namespace Models
{
    [Serializable]
    public class McpConfig
    {
        [JsonProperty("mcpServers")]
        public McpConfigServers mcpServers;
    }
}
