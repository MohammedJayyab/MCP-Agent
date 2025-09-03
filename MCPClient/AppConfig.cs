using Microsoft.Extensions.Configuration;

namespace MCPClient
{
    public static class AppConfig
    {
        private static IConfiguration? _configuration;

        public static void Initialize()
        {
            _configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();
                
            ClientConfig.Initialize(_configuration);
        }

        public static string GetDefaultProvider()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Configuration not initialized. Call AppConfig.Initialize() first.");
            }

            return _configuration["LLM:DefaultProvider"] ?? "Gemini";
        }

        public static string GetDefaultTemperature()
        {
            return _configuration?["LLM:DefaultTemparature"] ?? "0.3";
        }

        public static string GetDefaultMaxTokens()
        {
            return _configuration?["LLM:DefaultMaxTokens"] ?? "4096";
        }

        public static string GetOpenAIApiKey()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Configuration not initialized. Call AppConfig.Initialize() first.");
            }

            var apiKey = _configuration["LLM:OpenAI:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey) || apiKey == "your-openai-key")
            {
                throw new InvalidOperationException(
                    "OpenAI API key not configured. Please update the 'LLM:OpenAI:ApiKey' value in appsettings.json");
            }

            return apiKey;
        }

        public static string GetOpenAIModel()
        {
            return _configuration?["LLM:OpenAI:Model"] ?? "gpt-3.5-turbo";
        }

        public static string GetDeepSeekApiKey()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Configuration not initialized. Call AppConfig.Initialize() first.");
            }

            var apiKey = _configuration["LLM:DeepSeek:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey) || apiKey == "your-deepseek-key")
            {
                throw new InvalidOperationException(
                    "DeepSeek API key not configured. Please update the 'LLM:DeepSeek:ApiKey' value in appsettings.json");
            }

            return apiKey;
        }

        public static string GetDeepSeekModel()
        {
            return _configuration?["LLM:DeepSeek:Model"] ?? "deepseek-chat";
        }

        public static string GetGeminiApiKey()
        {
            if (_configuration == null)
            {
                throw new InvalidOperationException("Configuration not initialized. Call AppConfig.Initialize() first.");
            }

            var apiKey = _configuration["LLM:Gemini:ApiKey"];
            
            if (string.IsNullOrEmpty(apiKey) || apiKey == "your-gemini-key")
            {
                throw new InvalidOperationException(
                    "Gemini API key not configured. Please update the 'LLM:Gemini:ApiKey' value in appsettings.json");
            }

            return apiKey;
        }

        public static string GetGeminiModel()
        {
            return _configuration?["LLM:Gemini:Model"] ?? "gemini-1.5-flash";
        }
    }
}
