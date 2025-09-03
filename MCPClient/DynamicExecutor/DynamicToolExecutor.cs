using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq; // Added for .Select()

namespace MCPClient.DynamicExecutor
{
    public class DynamicToolExecutor
    {
        private readonly HttpClient _httpClient;
        private readonly ToolDiscoverer _toolDiscoverer;

        public DynamicToolExecutor(ToolDiscoverer toolDiscoverer)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(ClientConfig.RequestTimeoutSeconds);
            _toolDiscoverer = toolDiscoverer;
        }

        public async Task<JsonElement> ExecuteTool(string toolName, Dictionary<string, object> parameters)
        {
            try
            {
                // 1. Get tool definition dynamically from discovered tools
                var tool = _toolDiscoverer.GetTool(toolName);
                if (tool == null)
                {
                    throw new ArgumentException($"Tool '{toolName}' not found. Available tools: {string.Join(", ", _toolDiscoverer.GetAvailableTools().Select(t => t.Name))}");
                }

                // 2. Validate parameters against discovered schema
                ValidateParameters(tool, parameters);

                // 3. Build request dynamically using tool definition
                var request = BuildDynamicRequest(tool, parameters);

                // 4. Send and return response
                return await SendRequest(request);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"Failed to execute tool '{toolName}': {ex.Message}");
                throw;
            }
        }

        private void ValidateParameters(ToolDefinition tool, Dictionary<string, object> parameters)
        {
            // Check if tool has required parameters
            if (tool.Parameters.Count > 0)
            {
                foreach (var param in tool.Parameters)
                {
                    if (!parameters.ContainsKey(param.Key))
                    {
                        throw new ArgumentException($"Required parameter '{param.Key}' is missing for tool '{tool.Name}'. Description: {param.Value.Description}");
                    }
                }
            }

            // Check for extra parameters
            foreach (var param in parameters)
            {
                if (!tool.Parameters.ContainsKey(param.Key))
                {
                    throw new ArgumentException($"Unknown parameter '{param.Key}' for tool '{tool.Name}'. Available parameters: {string.Join(", ", tool.Parameters.Keys)}");
                }
            }
        }

        private object BuildDynamicRequest(ToolDefinition tool, Dictionary<string, object> parameters)
        {
            return new
            {
                jsonrpc = ClientConfig.JsonRpcVersion,
                method = tool.Name,  // Dynamic method name
                @params = parameters, // Dynamic parameters
                id = GenerateRequestId()
            };
        }

        private async Task<JsonElement> SendRequest(object request)
        {
            var jsonRequest = JsonSerializer.Serialize(request);
            var content = new StringContent(jsonRequest, Encoding.UTF8, ClientConfig.ContentType);

            Console.ForegroundColor = ClientConfig.Colors.Info;
            Console.WriteLine($"Executing: {jsonRequest}");

            var response = await _httpClient.PostAsync(ClientConfig.ServerUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"HTTP {response.StatusCode}: {responseContent}");
            }

            var jsonResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);

            // Check for JSON-RPC error
            if (jsonResponse.TryGetProperty("error", out var error))
            {
                var errorMessage = error.TryGetProperty("message", out var msg) ? msg.GetString() : "Unknown error";
                throw new Exception($"JSON-RPC Error: {errorMessage}");
            }

            // Return result
            if (jsonResponse.TryGetProperty("result", out var result))
            {
                Console.ForegroundColor = ClientConfig.Colors.Success;
                Console.WriteLine($"Success: {result}");
                return result;
            }

            throw new Exception("No result in response");
        }

        private string GenerateRequestId()
        {
            return $"exec-{Guid.NewGuid().ToString("N")[..8]}";
        }
    }
}
