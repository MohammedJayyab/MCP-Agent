using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPServer
{
    // Standard JSON-RPC request structure
    public class JsonRpcRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("method")]
        public string Method { get; set; }

        [JsonPropertyName("params")]
        public JsonElement Params { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    // Standard JSON-RPC response structure
    public class JsonRpcResponse
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRpcError? Error { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    // Standard JSON-RPC error structure
    public class JsonRpcError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }
    }

    // Models for dynamic tool discovery (matching tools.json)
    public class ToolParameter
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "string";

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class ToolParametersSchema
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "object";

        [JsonPropertyName("properties")]
        public Dictionary<string, ToolParameter> Properties { get; set; } = new Dictionary<string, ToolParameter>();

        [JsonPropertyName("required")]
        public string[] Required { get; set; } = new string[0];
    }

    public class ToolDefinition
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("parameters")]
        public ToolParametersSchema Parameters { get; set; } = new ToolParametersSchema();
    }

    public class ToolListResponse
    {
        [JsonPropertyName("tools")]
        public List<ToolDefinition> Tools { get; set; } = new List<ToolDefinition>();
    }
}