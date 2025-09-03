using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCPServer
{
    public class Server
    {
        private readonly HttpListener _listener;
        private readonly IDatabaseService _databaseService;
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public Server(string url, IDatabaseService databaseService)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(url);
            _databaseService = databaseService;
            _jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true
            };
        }

        public async Task StartAsync(CancellationToken cancellationToken = default)
        {
            _listener.Start();
            Console.WriteLine($"RPC Server is listening on {_listener.Prefixes.First()}");

            while (_listener.IsListening && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequestAsync(context));
                }
                catch (HttpListenerException ex)
                {
                    Console.WriteLine($"HttpListener error: {ex.Message}");
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            _listener.Stop();
            _listener.Close();
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            string responseJson;
            var request = context.Request;
            var httpResponse = context.Response;

            try
            {
                // Add CORS headers
                httpResponse.Headers.Add("Access-Control-Allow-Origin", "*");
                httpResponse.Headers.Add("Access-Control-Allow-Methods", "POST, OPTIONS");
                httpResponse.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

                // Handle preflight OPTIONS request
                if (request.HttpMethod == "OPTIONS")
                {
                    httpResponse.StatusCode = 200;
                    return;
                }

                if (request.HttpMethod != "POST")
                {
                    throw new InvalidOperationException("Only HTTP POST requests are supported for JSON-RPC.");
                }

                // Validate request size (limit to 1MB)
                if (request.ContentLength64 > 1024 * 1024)
                {
                    throw new InvalidOperationException("Request body too large. Maximum size is 1MB.");
                }

                using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
                var requestBody = await reader.ReadToEndAsync();

                Console.WriteLine($"Received Request Body: {requestBody}");

                var jsonRpcRequest = JsonSerializer.Deserialize<JsonRpcRequest>(requestBody, _jsonSerializerOptions);

                if (jsonRpcRequest == null || string.IsNullOrEmpty(jsonRpcRequest.Method))
                {
                    throw new JsonException("Invalid JSON-RPC request format: Missing method or malformed JSON.");
                }

                object? methodResult = null;

                switch (jsonRpcRequest.Method)
                {
                    case "health":
                        methodResult = new { status = "healthy", timestamp = DateTime.UtcNow };
                        break;

                    case "executeSQL":
                        if (!jsonRpcRequest.Params.TryGetProperty("query", out var queryElement))
                        {
                            throw new ArgumentException("Parameter 'query' is required for executeSQL.");
                        }
                        var query = queryElement.GetString() ??
                                    throw new ArgumentException("Parameter 'query' cannot be null or empty for executeSQL.");
                        methodResult = _databaseService.ExecuteSQL(query);
                        break;

                    case "getTableSchema":
                        if (!jsonRpcRequest.Params.TryGetProperty("tableName", out var tableNameElement))
                        {
                            throw new ArgumentException("Parameter 'tableName' is required for getTableSchema.");
                        }
                        var tableName = tableNameElement.GetString() ??
                                        throw new ArgumentException("Parameter 'tableName' cannot be null or empty for getTableSchema.");
                        methodResult = _databaseService.GetTableSchema(tableName);
                        break;

                    case "getDatabaseSchema":
                        methodResult = _databaseService.GetDatabaseSchema();
                        break;

                    case "getTableColumns":
                        if (!jsonRpcRequest.Params.TryGetProperty("tableName", out var tableNameForColumnsElement))
                        {
                            throw new ArgumentException("Parameter 'tableName' is required for getTableColumns.");
                        }
                        var tableNameForColumns = tableNameForColumnsElement.GetString() ??
                                        throw new ArgumentException("Parameter 'tableName' cannot be null or empty for getTableColumns.");
                        methodResult = _databaseService.GetTableColumns(tableNameForColumns);
                        break;

                    case "getTools":
                        methodResult = GetToolsDefinition();
                        break;

                    default:
                        throw new InvalidOperationException($"Method '{jsonRpcRequest.Method}' not found.");
                }

                var resultElement = JsonSerializer.SerializeToElement(methodResult, _jsonSerializerOptions);
                var successResponse = new JsonRpcResponse { Id = jsonRpcRequest.Id, Result = resultElement };
                responseJson = JsonSerializer.Serialize(successResponse, _jsonSerializerOptions);
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"JSON Parsing Error: {ex.Message}");
                var error = new JsonRpcError { Code = -32700, Message = "Parse error: Invalid JSON was received by the server." };
                var errorResponse = new JsonRpcResponse { Id = null, Error = error };
                responseJson = JsonSerializer.Serialize(errorResponse, _jsonSerializerOptions);
            }
            catch (ArgumentException ex)
            {
                Console.WriteLine($"Argument Error: {ex.Message}");
                var error = new JsonRpcError { Code = -32602, Message = $"Invalid params: {ex.Message}" };
                var errorResponse = new JsonRpcResponse { Id = null, Error = error };
                responseJson = JsonSerializer.Serialize(errorResponse, _jsonSerializerOptions);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"Server Logic Error: {ex.Message}");
                var error = new JsonRpcError { Code = -32601, Message = ex.Message };
                var errorResponse = new JsonRpcResponse { Id = null, Error = error };
                responseJson = JsonSerializer.Serialize(errorResponse, _jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An unhandled exception occurred: {ex.Message}");
                var error = new JsonRpcError { Code = -32000, Message = "Server error: An unexpected error occurred." };
                var errorResponse = new JsonRpcResponse { Id = null, Error = error };
                responseJson = JsonSerializer.Serialize(errorResponse, _jsonSerializerOptions);
            }

            httpResponse.ContentType = "application/json";
            httpResponse.ContentEncoding = Encoding.UTF8;
            var buffer = Encoding.UTF8.GetBytes(responseJson);
            httpResponse.ContentLength64 = buffer.Length;
            await httpResponse.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            httpResponse.OutputStream.Close();
            Console.WriteLine($"Sent Response Body: {responseJson}");
        }

        private object GetToolsDefinition()
        {
            try
            {
                var toolsJson = File.ReadAllText("tools.json");
                return JsonSerializer.Deserialize<object>(toolsJson, _jsonSerializerOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading tools.json: {ex.Message}");
                return new { error = "Failed to load tools definition" };
            }
        }
    }
}