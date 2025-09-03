using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using LLMKit;
using LLMKit.Providers;
using MCPClient.DynamicExecutor;

namespace MCPClient.LLMOrchestrator
{
    public class LLMOrchestrator
    {
        private readonly LLMClient _llmClient;
        private readonly DynamicToolExecutor _toolExecutor;
        private readonly ToolDiscoverer _toolDiscoverer;
        private readonly List<ToolDefinition> _availableTools;

        public LLMOrchestrator(LLMClient llmClient, DynamicToolExecutor toolExecutor, ToolDiscoverer toolDiscoverer)
        {
            _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _toolDiscoverer = toolDiscoverer ?? throw new ArgumentNullException(nameof(toolDiscoverer));
            _availableTools = toolDiscoverer.GetAvailableTools();
            
            // Set system prompt for intelligent tool orchestration
            SetOrchestratorSystemPrompt();
        }

        private void SetOrchestratorSystemPrompt()
        {
            var toolsDescription = GenerateToolsDescription();
            
            var systemPrompt = $@"You are an intelligent database assistant that helps users by calling the appropriate tools based on their requests.

                    AVAILABLE TOOLS:
                    {toolsDescription}

                    IMPORTANT RULES:
                    1. NEVER guess table names, column names, or SQL queries
                    2. ONLY use the tools listed above - do not invent or assume any tools exist
                    3. ALWAYS ask for clarification if you need specific table names or column names
                    4. Use tools in the correct sequence to gather information before answering                  
                    5. Some queries can be answered without knowning the column names.
                   
                    
                   
              

                    RESPONSE FORMAT:
                    When you need to call tools, respond with this exact JSON format:
                    {{
                        ""action"": ""call_tool"",
                        ""tool_name"": ""exact_tool_name_from_list"",
                        ""parameters"": {{ ""param_name"": ""param_value"" }},
                        ""reason"": ""why you are calling this tool"",
                        
                    }}

                    When you have enough information to answer the user, respond with:
                    {{
                        ""action"": ""answer_user"",
                        ""response"": ""your detailed answer based on the tool results""
                    }}

                    When you need clarification, respond with:
                    {{
                        ""action"": ""ask_clarification"",
                        ""question"": ""what specific information you need""
                    }}";

            _llmClient.SetSystemMessage(systemPrompt);
        }

        private string GenerateToolsDescription()
        {
            var description = new System.Text.StringBuilder();
            
            foreach (var tool in _availableTools)
            {
                description.AppendLine($"- {tool.Name}: {tool.Description}");
                if (tool.Parameters.Count > 0)
                {
                    description.AppendLine("  REQUIRED Parameters:");
                    foreach (var param in tool.Parameters)
                    {
                        description.AppendLine($"    • {param.Key} ({param.Value.Type}): {param.Value.Description}");
                    }
                }
                else
                {
                    description.AppendLine("  Parameters: none");
                }
                description.AppendLine();
            }
            
            return description.ToString();
        }

        public async Task<string> ProcessUserRequest(string userPrompt)
        {
            try
            {
                var originalUserPrompt = userPrompt;
                Console.ForegroundColor = ClientConfig.Colors.Info;
                Console.WriteLine($"Processing user request: {userPrompt}");
                Console.WriteLine();

                var conversationHistory = new List<string>();
                var maxIterations = 10; // Prevent infinite loops
                var iteration = 0;

                while (iteration < maxIterations)
                {
                    iteration++;
                    Console.WriteLine($"--- Iteration {iteration} ---");

                    // Get LLM decision                   

                    var llmResponse = await _llmClient.GenerateTextAsync(userPrompt);
                   

                    conversationHistory.Add($"LLM Response: {llmResponse}");

                    // Parse LLM response
                    var decision = ParseLLMResponse(llmResponse);
                    
                    switch (decision.Action)
                    {
                        case "call_tool":
                            var toolResult = await ExecuteToolCall(decision);
                            conversationHistory.Add($"Tool Result: {toolResult}");
                            
                            // Add tool result to conversation context for next iteration
                            userPrompt = $"Answer: '{originalUserPrompt}' based on result from: Previous tool '{decision.ToolName}' : returned: {toolResult}.";
                          //userPrompt = $"Answer: '{originalUserPrompt}' based on  history: '{string.Join("\r\n", conversationHistory)}'";
                            break;

                        case "answer_user":
                            //Console.ForegroundColor = ClientConfig.Colors.Success;
                            //Console.WriteLine("Final Answer:");
                            //Console.WriteLine(decision.Response);
                            return decision.Response;

                        case "ask_clarification":
                            Console.ForegroundColor = ClientConfig.Colors.Warning;
                            Console.WriteLine("Need clarification:");
                            Console.WriteLine(decision.Question);
                            return $"I need more information: {decision.Question}";

                        default:
                            Console.ForegroundColor = ClientConfig.Colors.Error;
                            Console.WriteLine("Invalid LLM response format");
                            return "Sorry, I couldn't process your request properly.";
                    }
                }

                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine("Maximum iterations reached");
                return "Sorry, I couldn't complete your request within the allowed iterations.";
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"Error processing request: {ex.Message}");
                return $"Sorry, an error occurred: {ex.Message}";
            }
        }

        private async Task<string> ExecuteToolCall(LLMDecision decision)
        {
            try
            {
                Console.ForegroundColor = ClientConfig.Colors.Info;
                Console.WriteLine($"Calling tool: {decision.ToolName}");
                Console.WriteLine($"Reason: {decision.Reason}");
                Console.WriteLine($"Parameters: {JsonSerializer.Serialize(decision.Parameters)}");

                var result = await _toolExecutor.ExecuteTool(decision.ToolName, decision.Parameters);
                return result.ToString();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"Tool execution failed: {ex.Message}");
                return $"ERROR: {ex.Message}";
            }
        }

        private LLMDecision ParseLLMResponse(string response)
        {
            try
            {
                // Try to parse as JSON first
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(response);
                
                if (jsonResponse.TryGetProperty("action", out var action))
                {
                    var actionValue = action.GetString();
                    
                    switch (actionValue)
                    {
                        case "call_tool":
                            return new LLMDecision
                            {
                                Action = "call_tool",
                                ToolName = jsonResponse.GetProperty("tool_name").GetString() ?? "",
                                Parameters = ParseParameters(jsonResponse.GetProperty("parameters")),
                                Reason = jsonResponse.GetProperty("reason").GetString() ?? ""
                            };

                        case "answer_user":
                            return new LLMDecision
                            {
                                Action = "answer_user",
                                Response = jsonResponse.GetProperty("response").GetString() ?? ""
                            };

                        case "ask_clarification":
                            return new LLMDecision
                            {
                                Action = "ask_clarification",
                                Question = jsonResponse.GetProperty("question").GetString() ?? ""
                            };
                    }
                }
            }
            catch (JsonException)
            {
                // If JSON parsing fails, treat as a regular response
                Console.ForegroundColor = ClientConfig.Colors.Warning;
                Console.WriteLine("LLM response is not in JSON format, treating as answer");
            }

            // Default: treat as user answer
            return new LLMDecision
            {
                Action = "answer_user",
                Response = response
            };
        }

        private Dictionary<string, object> ParseParameters(JsonElement parametersElement)
        {
            var parameters = new Dictionary<string, object>();
            
            foreach (var property in parametersElement.EnumerateObject())
            {
                object value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString() ?? "",
                    JsonValueKind.Number => property.Value.GetInt32(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    _ => property.Value.GetString() ?? ""
                };
                
                parameters[property.Name] = value;
            }
            
            return parameters;
        }
    }

    public class LLMDecision
    {
        public string Action { get; set; } = "";
        public string ToolName { get; set; } = "";
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string Reason { get; set; } = "";
        public string Response { get; set; } = "";
        public string Question { get; set; } = "";
    }
}
