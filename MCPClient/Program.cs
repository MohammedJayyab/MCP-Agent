using System;
using System.Threading.Tasks;
using MCPClient.DynamicExecutor;
using MCPClient.LLMOrchestrator;

namespace MCPClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                AppConfig.Initialize();

                Console.WriteLine($"Server URL: {ClientConfig.ServerUrl}");

                var healthChecker = new HealthChecker();
                if (!await healthChecker.CheckServerHealth())
                {
                    Console.ForegroundColor = ClientConfig.Colors.Error;
                    Console.WriteLine("Server is not responding");
                    return;
                }

                Console.ForegroundColor = ClientConfig.Colors.Success;
                Console.WriteLine("Server is healthy!");

                var toolDiscoverer = new ToolDiscoverer();
                if (!await toolDiscoverer.DiscoverTools())
                {
                    Console.ForegroundColor = ClientConfig.Colors.Error;
                    Console.WriteLine("Failed to discover tools");
                    return;
                }

                var executor = new DynamicToolExecutor(toolDiscoverer);
                var orchestrator = new LLMOrchestratorInterface();
                orchestrator.SetupOrchestrator(executor, toolDiscoverer);

                Console.ForegroundColor = ClientConfig.Colors.Info;
                Console.WriteLine($"Using {AppConfig.GetDefaultProvider()} as LLM provider");
                Console.WriteLine("🤖 LLM Orchestrator ready! Type 'exit' to quit.");

                await RunInteractiveLoop(orchestrator);
            }
            /*catch (LLMKit.Exceptions.LLMException llmEx)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"LLM Error: {llmEx.Message} \r\n Des. : {llmEx.ToString()}");
            }*/
            catch (Exception ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ReadLine();
        }

        private static async Task RunInteractiveLoop(LLMOrchestratorInterface orchestrator)
        {
            while (true)
            {
                Console.ForegroundColor = ClientConfig.Colors.Warning;
                Console.Write("❓ Question: ");
                Console.ForegroundColor = ConsoleColor.White;

                var question = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(question)) continue;

                if (question.ToLower() == "exit")
                {
                    Console.ForegroundColor = ClientConfig.Colors.Info;
                    Console.WriteLine("👋 Goodbye!");
                    break;
                }

                try
                {
                    await orchestrator.AskQuestion(question);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ClientConfig.Colors.Error;
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }
            }
        }
    }
}