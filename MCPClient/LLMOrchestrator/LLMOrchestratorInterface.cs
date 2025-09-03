using System;
using System.Threading.Tasks;
using LLMKit;
using LLMKit.Providers;
using MCPClient.DynamicExecutor;

namespace MCPClient.LLMOrchestrator
{
    public class LLMOrchestratorInterface
    {
        private LLMOrchestrator? _orchestrator;
        private readonly LLMClient _llmClient;
        private readonly string _providerName;
        private readonly string _modelName;

        public LLMOrchestratorInterface()
        {
            var defaultProvider = AppConfig.GetDefaultProvider();
            var temperature = AppConfig.GetDefaultTemperature();
            var maxTokens = AppConfig.GetDefaultMaxTokens();
            
            ILLMProvider provider;
            
            switch (defaultProvider.ToLower())
            {
                case "openai":
                    var openAiApiKey = AppConfig.GetOpenAIApiKey();
                    var openAiModel = AppConfig.GetOpenAIModel();
                    provider = new OpenAIProvider(openAiApiKey, openAiModel);
                    _providerName = "OpenAI";
                    _modelName = openAiModel;
                    break;
                    
                case "deepseek":
                    var deepSeekApiKey = AppConfig.GetDeepSeekApiKey();
                    var deepSeekModel = AppConfig.GetDeepSeekModel();
                    provider = new DeepSeekProvider(deepSeekApiKey, deepSeekModel);
                    _providerName = "DeepSeek";
                    _modelName = deepSeekModel;
                    break;
                    
                case "gemini":
                    var geminiApiKey = AppConfig.GetGeminiApiKey();
                    var geminiModel = AppConfig.GetGeminiModel();
                    provider = new GeminiProvider(geminiApiKey, geminiModel);
                    _providerName = "Gemini";
                    _modelName = geminiModel;
                    break;
                    
                default:
                    throw new InvalidOperationException($"Unsupported LLM provider: {defaultProvider}");
            }
            
            // Parse configuration values
            if (!float.TryParse(temperature, out var tempValue))
            {
                tempValue = 0.3f; // Default fallback
            }
            
            if (!int.TryParse(maxTokens, out var maxTokensValue))
            {
                maxTokensValue = 4096; // Default fallback
            }
            
            // Create LLMClient with custom parameters
            _llmClient = new LLMClient(
                provider: provider,
                maxTokens: maxTokensValue,
                temperature: tempValue,
                maxMessages: 30 // Store up to 30 messages in conversation history
            );
            
            Console.ForegroundColor = ClientConfig.Colors.Info;
            Console.ForegroundColor = ClientConfig.Colors.Success;
            Console.WriteLine($"LLM Configuration - Provider: {_providerName}, Model: {_modelName}, Temperature: {tempValue}, MaxTokens: {maxTokensValue}");
            Console.ResetColor();
        }

        public void SetupOrchestrator(DynamicToolExecutor toolExecutor, ToolDiscoverer toolDiscoverer)
        {
            _orchestrator = new LLMOrchestrator(_llmClient, toolExecutor, toolDiscoverer);
        }

        public async Task<string> AskQuestion(string question)
        {
            if (_orchestrator == null)
            {
                throw new InvalidOperationException("Orchestrator not set up. Call SetupOrchestrator first.");
            }

            Console.ForegroundColor = ClientConfig.Colors.Info;
            Console.WriteLine($"\n🤖 LLM Orchestrator Processing Question:");
            Console.WriteLine($"Q: {question}");
            Console.WriteLine();

            var response = await _orchestrator.ProcessUserRequest(question);
            
            Console.ForegroundColor = ClientConfig.Colors.Success;
            Console.WriteLine($"\n✅ Final Answer:");
            Console.WriteLine(response);
            
            return response;
        }

        public void ShowOrchestratorInfo()
        {
            Console.ForegroundColor = ClientConfig.Colors.Info;
            Console.WriteLine("\n🤖 LLM Orchestrator Information:");
            Console.WriteLine($"- LLM Provider: {_providerName}");
            Console.WriteLine($"- Model: {_modelName}");
            Console.WriteLine($"- Settings: {_llmClient.GetAllSettings()}");
            Console.WriteLine("- Capabilities: Intelligent tool selection and execution");
            Console.WriteLine("- Features: Multi-step reasoning, parameter validation, error handling");
        }
    }
}
