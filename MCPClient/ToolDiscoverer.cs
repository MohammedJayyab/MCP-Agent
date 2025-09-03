using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MCPClient
{
    public class ToolDiscoverer
    {
        private readonly HttpClient _httpClient;
        private List<ToolDefinition> _availableTools;

        public ToolDiscoverer()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(ClientConfig.RequestTimeoutSeconds);
            _availableTools = new List<ToolDefinition>();
        }

        public async Task<bool> DiscoverTools()
        {
            try
            {
                Console.ForegroundColor = ClientConfig.Colors.Info;
                Console.WriteLine($"Sending tool discovery request to: {ClientConfig.ServerUrl}");
                
                var toolsRequest = new
                {
                    jsonrpc = ClientConfig.JsonRpcVersion,
                    method = "getTools",
                    @params = new { },
                    id = ClientConfig.ToolsDiscoveryId
                };

                var jsonRequest = JsonSerializer.Serialize(toolsRequest);
                var content = new StringContent(jsonRequest, Encoding.UTF8, ClientConfig.ContentType);

                Console.WriteLine($"Request payload: {jsonRequest}");
                Console.WriteLine($"Timeout: {ClientConfig.RequestTimeoutSeconds} seconds");

                var response = await _httpClient.PostAsync(ClientConfig.ServerUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                Console.WriteLine($"Response status: {response.StatusCode}");
                Console.WriteLine($"Response content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var toolsResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (toolsResponse.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("tools", out var toolsArray))
                    {
                        _availableTools.Clear();
                        
                        foreach (var toolElement in toolsArray.EnumerateArray())
                        {
                            var tool = new ToolDefinition
                            {
                                Name = toolElement.GetProperty("name").GetString() ?? "",
                                Description = toolElement.GetProperty("description").GetString() ?? "",
                                Parameters = ParseParameters(toolElement.GetProperty("parameters"))
                            };
                            _availableTools.Add(tool);
                        }

                        Console.ForegroundColor = ClientConfig.Colors.Success;
                        Console.WriteLine($"✅ Discovered {_availableTools.Count} tools from server");
                        return true;
                    }
                    else
                    {
                        Console.ForegroundColor = ClientConfig.Colors.Warning;
                        Console.WriteLine("⚠️ Response received but no tools found in result");
                        return false;
                    }
                }
                else
                {
                    Console.ForegroundColor = ClientConfig.Colors.Error;
                    Console.WriteLine($"❌ HTTP Error: {response.StatusCode} - {responseContent}");
                    return false;
                }
            }
            catch (TaskCanceledException ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"⏰ Timeout: Request took longer than {ClientConfig.RequestTimeoutSeconds} seconds");
                Console.WriteLine($"Error details: {ex.Message}");
                return false;
            }
            catch (HttpRequestException ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"🌐 Network Error: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"❌ Failed to discover tools: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                return false;
            }
        }

        public List<ToolDefinition> GetAvailableTools()
        {
            return _availableTools;
        }

        public ToolDefinition? GetTool(string toolName)
        {
            return _availableTools.Find(t => t.Name.Equals(toolName, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, ToolParameter> ParseParameters(JsonElement parametersElement)
        {
            var parameters = new Dictionary<string, ToolParameter>();
            
            if (parametersElement.TryGetProperty("properties", out var properties))
            {
                foreach (var property in properties.EnumerateObject())
                {
                    var param = new ToolParameter
                    {
                        Type = property.Value.GetProperty("type").GetString() ?? "string",
                        Description = property.Value.GetProperty("description").GetString() ?? ""
                    };
                    parameters[property.Name] = param;
                }
            }
            else
            {
                // Try parsing parameters directly if they're not in a properties object
                foreach (var property in parametersElement.EnumerateObject())
                {
                    var param = new ToolParameter
                    {
                        Type = property.Value.GetProperty("type").GetString() ?? "string",
                        Description = property.Value.GetProperty("description").GetString() ?? ""
                    };
                    parameters[property.Name] = param;
                }
            }

            return parameters;
        }
    }

    public class ToolDefinition
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public Dictionary<string, ToolParameter> Parameters { get; set; } = new Dictionary<string, ToolParameter>();
    }

    public class ToolParameter
    {
        public string Type { get; set; } = "string";
        public string Description { get; set; } = "";
    }
}
