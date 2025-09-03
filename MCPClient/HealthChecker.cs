using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MCPClient
{
    public class HealthChecker
    {
        private readonly HttpClient _httpClient;

        public HealthChecker()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(ClientConfig.RequestTimeoutSeconds);
        }

        public async Task<bool> CheckServerHealth()
        {
            try
            {
                var healthRequest = new
                {
                    jsonrpc = ClientConfig.JsonRpcVersion,
                    method = "health",
                    @params = new { },
                    id = ClientConfig.HealthCheckId
                };

                var jsonRequest = JsonSerializer.Serialize(healthRequest);
                var content = new StringContent(jsonRequest, Encoding.UTF8, ClientConfig.ContentType);

                var response = await _httpClient.PostAsync(ClientConfig.ServerUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var healthResponse = JsonSerializer.Deserialize<JsonElement>(responseContent);
                    
                    if (healthResponse.TryGetProperty("result", out var result))
                    {
                        if (result.TryGetProperty("status", out var status) && 
                            status.GetString() == "healthy")
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
