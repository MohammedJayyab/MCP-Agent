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

            var systemPrompt = $@"You are an intelligent assistant that helps users by calling the appropriate tools based on their requests.

                    CRITICAL INSTRUCTION: You MUST ALWAYS respond with valid JSON format. NEVER use natural language responses.
                    Your response must be parseable JSON starting with {{ and ending with }}.
                    DO NOT wrap your response in markdown code blocks (```json) or any other formatting.

                    AVAILABLE TOOLS:
                    {toolsDescription}

                    CRITICAL RULES - NEVER VIOLATE THESE:
                    1. NEVER guess or assume names, structures, or data that you haven't discovered
                    2. NEVER invent or assume any tools exist beyond what's listed above
                    3. If you don't know something, use tools to find out - don't guess
                    4. Only proceed with specific operations after you have confirmed the information exists

                    RESPONSE FORMAT:
                    The ""action"" field must be exactly one of: ""call_tool"", ""answer_user"", or ""ask_clarification""
                    The ""tool_name"" field contains the specific tool you want to use from the available tools list

                    When you need to call tools, respond with this exact JSON format:
                    {{
                        ""action"": ""call_tool"",
                        ""tool_name"": ""tool_name_from_available_list"",
                        ""parameters"": {{ ""param_name"": ""param_value"" }},
                        ""reason"": ""why you are calling this tool""
                    }}

                    When you have enough information to answer the user, respond with:
                    {{
                        ""action"": ""answer_user"",
                        ""response"": ""your detailed answer based on the tool results, and consider to fix the user query if it was not correct.""
                    }}

                    When you need clarification, respond with:
                    {{
                        ""action"": ""ask_clarification"",
                        ""question"": ""what specific information you need""
                    }}
                    Note: The ""action"" field must be exactly one of: ""call_tool"", ""answer_user"", or ""ask_clarification"". Do not change these values.

                    The ""tool_name"" field should contain the actual name of the tool you want to call as listed in AVAILABLE TOOLS.

                    FINAL REMINDER: Your response MUST be valid JSON starting with {{ and ending with }}. No natural language allowed.

                    SMART TOOL USAGE STRATEGY:
                    1. Analyze the available tools and their descriptions to understand what each can do
                    2. Use tools to understand the structure before attempting operations
                    3. Only call operation tools after you have confirmed the target exists
                    4. If a tool fails or returns errors, use other tools to investigate why
                    5. Build your knowledge step by step through tool calls
                    6. use the history of the conversation to fetch needed information";

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

                var maxIterations = 10; // Prevent infinite loops
                var iteration = 0;

                while (iteration < maxIterations)
                {
                    iteration++;
                    Console.WriteLine($"--- Iteration {iteration} ---");

                    // First iteration: Let LLM analyze the user query and decide on tools
                    if (iteration == 1)

                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(">>> Analyzing user question and available tools...");
                        var toolsSummary = string.Join(", ", _availableTools.Select(t => t.Name));
                        userPrompt = $@"User Question: {originalUserPrompt}

                                Available Tools: {toolsSummary}
                                Analyze the user's question and decide which  tool to call first.";
                    }
                    else
                    {
                        // Subsequent iterations: Build context from previous tool results
                        userPrompt = $@"Original Question: {originalUserPrompt}

                        Previous Tool Results:
                        {userPrompt}

                        Based on the tool results above, determine your next step.
                        Remember: decide which  tool to call first.";
                    }

                    var llmResponse = await _llmClient.GenerateTextAsync(userPrompt);

                    // Log LLM response for debugging
                    Console.ForegroundColor = ClientConfig.Colors.Info;
                    Console.WriteLine($"LLM Response: {llmResponse}");
                    Console.WriteLine();

                    // Parse LLM response
                    var decision = ParseLLMResponse(llmResponse);
                    Thread.Sleep(1000); // Brief pause for readability

                    switch (decision.Action)
                    {
                        case "call_tool":
                            var toolResult = await ExecuteToolCall(decision);

                            // Build context for next iteration
                            userPrompt = $@"Tool '{decision.ToolName}' returned: {toolResult}";
                            break;

                        case "answer_user":
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
            catch (LLMKit.Exceptions.LLMException llmEx)
            {
                Console.ForegroundColor = ClientConfig.Colors.Error;
                Console.WriteLine($"LLM Error: {llmEx.Message} \r\n Des. : {llmEx.ToString()}");
                return $"Sorry, an LLM error occurred: {llmEx.Message}";
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
                // Validate tool name exists
                var toolExists = _availableTools.Any(t => t.Name.Equals(decision.ToolName, StringComparison.OrdinalIgnoreCase));
                if (!toolExists)
                {
                    var availableToolNames = string.Join(", ", _availableTools.Select(t => t.Name));
                    return $"ERROR: Tool '{decision.ToolName}' not found. Available tools: {availableToolNames}. Please use only the tools listed above.";
                }

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
                // Clean the response to extract JSON from markdown code blocks or other formatting
                var cleanedResponse = CleanResponse(response);

                // Try to parse as JSON
                var jsonResponse = JsonSerializer.Deserialize<JsonElement>(cleanedResponse);

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
            catch (JsonException ex)
            {
                // If JSON parsing fails, log the actual response for debugging
                Console.ForegroundColor = ClientConfig.Colors.Warning;
                Console.WriteLine($"LLM response is not in JSON format:");
                Console.WriteLine($"Raw response: {response}");
                Console.WriteLine($"JSON parsing error: {ex.Message}");
                Console.WriteLine("Treating as answer_user response");
            }

            // Default: treat as user answer
            return new LLMDecision
            {
                Action = "answer_user",
                Response = response
            };
        }

        private string CleanResponse(string response)
        {
            // Remove markdown code blocks
            if (response.Contains("```json"))
            {
                var startIndex = response.IndexOf("```json") + 7;
                var endIndex = response.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    response = response.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
            else if (response.Contains("```"))
            {
                var startIndex = response.IndexOf("```") + 3;
                var endIndex = response.LastIndexOf("```");
                if (endIndex > startIndex)
                {
                    response = response.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }

            // Remove any leading/trailing whitespace and newlines
            response = response.Trim();

            // If the response starts with a newline, remove it
            if (response.StartsWith("\n"))
            {
                response = response.TrimStart('\n');
            }

            return response;
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