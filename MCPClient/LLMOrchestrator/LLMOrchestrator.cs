using LLMKit;
using MCPClient.DynamicExecutor;
using System.Text.Json;

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
5. ALWAYS respond in the exact JSON format specified below - never use natural language
6. Think step by step: first explore the database structure, then execute queries

ANALYTICAL THINKING PROCESS:
- Start by deeply analyzing what the user wants to know and why
- Identify the core data requirements and potential data relationships
- Plan your investigative approach: what tools to call and in what strategic sequence
- Think about what you expect to find and what questions the data might raise
- Execute tools systematically while adapting your plan based on discoveries
- Synthesize information from multiple sources to build comprehensive, insightful answers
- Question your assumptions and validate understanding through data exploration
- Consider edge cases and alternative interpretations of the data
- If you encounter errors or missing information, ask targeted clarification questions

RESPONSE FORMAT - YOU MUST ALWAYS USE ONE OF THESE EXACT JSON FORMATS:

When you need to call a tool:
{{
    ""action"": ""call_tool"",
    ""tool_name"": ""exact_tool_name_from_list"",
    ""parameters"": {{ ""param_name"": ""param_value"" }},
    ""reason"": ""Clear explanation of why this tool is needed and what information it will provide""
}}

When you have enough information to answer the user:
{{
    ""action"": ""answer_user"",
    ""response"": ""Comprehensive, well-structured answer that directly addresses the user's question with clear explanations and insights from the data""
}}

When you need clarification:
{{
    ""action"": ""ask_clarification"",
    ""question"": ""Specific, focused question that will help you provide a better answer""
}}

CRITICAL: You must respond with ONLY valid JSON in one of these three formats. Never use natural language, explanations, or any text outside the JSON structure.
Remember: ""action"" is always the type (call_tool), ""tool_name"" is the actual tool name.";

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
                            userPrompt = $@"Original Question: {originalUserPrompt}

Previous Tool Results:
- Tool '{decision.ToolName}' returned: {toolResult}

Based on this information, analyze what you've learned and determine your next step:
1. Do you have enough information to answer the user's question comprehensively?
2. Do you need to call another tool to gather more data?
3. Do you need clarification about something specific?

Think strategically about what additional information would be most valuable and why.";
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
                            if (!jsonResponse.TryGetProperty("tool_name", out var toolName))
                            {
                                return new LLMDecision
                                {
                                    Action = "ask_clarification",
                                    Question = "Missing 'tool_name' field. Please include the tool name you want to call."
                                };
                            }

                            return new LLMDecision
                            {
                                Action = "call_tool",
                                ToolName = toolName.GetString() ?? "",
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

                        default:
                            return new LLMDecision
                            {
                                Action = "ask_clarification",
                                Question = $"Invalid action '{actionValue}'. Must be one of: 'call_tool', 'answer_user', or 'ask_clarification'."
                            };
                    }
                }
                else
                {
                    // Check if the LLM is using the wrong structure (putting tool name in action field)
                    if (jsonResponse.TryGetProperty("parameters", out var paramsElement) &&
                        jsonResponse.TryGetProperty("reason", out var reasonElement))
                    {
                        // This looks like a malformed call_tool response
                        return new LLMDecision
                        {
                            Action = "ask_clarification",
                            Question = "Invalid JSON structure. The 'action' field should be 'call_tool', not the tool name. Please use the correct format."
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