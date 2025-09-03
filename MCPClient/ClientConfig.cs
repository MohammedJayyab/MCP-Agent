using Microsoft.Extensions.Configuration;

namespace MCPClient
{
    public static class ClientConfig
    {
        private static IConfiguration? _configuration;
        
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }
        
        // Server Configuration
        public static string ServerHost => _configuration?["Server:Host"] ?? "localhost";
        public static int ServerPort => int.TryParse(_configuration?["Server:Port"], out var port) ? port : 8080;
        public static string ServerUrl => $"http://{ServerHost}:{ServerPort}/";
        
        // HTTP Configuration
        public const string ContentType = "application/json";
        public static int RequestTimeoutSeconds => int.TryParse(_configuration?["Server:RequestTimeoutSeconds"], out var timeout) ? timeout : 30;
        public static int MaxRequestSizeMB => int.TryParse(_configuration?["Server:MaxRequestSizeMB"], out var size) ? size : 1;
        
        // JSON-RPC Configuration
        public const string JsonRpcVersion = "2.0";
        
        // Request IDs
        public const string HealthCheckId = "health-check-1";
        public const string ToolsDiscoveryId = "tools-discovery-1";
        
        // Console Colors
        public static class Colors
        {
            public const ConsoleColor Success = ConsoleColor.Green;
            public const ConsoleColor Error = ConsoleColor.Red;
            public const ConsoleColor Info = ConsoleColor.Cyan;
            public const ConsoleColor Warning = ConsoleColor.Yellow;
        }
    }
}
