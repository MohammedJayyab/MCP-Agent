# MCP Client

A .NET client application that connects to an MCP Server and provides LLM-powered database interaction through multiple AI providers.

## Features

- **Multi-LLM Support**: OpenAI, DeepSeek, and Gemini integration
- **Dynamic Tool Discovery**: Automatically discovers available database tools from MCP Server
- **Interactive Console**: Natural language queries about your database
- **Health Monitoring**: Server health checks and connection management
- **Configurable**: Easy setup through configuration files

## Prerequisites

- .NET 8.0 or later
- Access to an MCP Server running on the configured host/port
- API keys for your preferred LLM providers

## Setup

### 1. Configuration Setup

**Copy the template configuration file:**
```bash
cp appsettings.template.json appsettings.json
```

**Update `appsettings.json` with your settings:**

```json
{
  "Server": {
    "Host": "localhost",
    "Port": 8080,
    "RequestTimeoutSeconds": 30,
    "MaxRequestSizeMB": 1
  },
  "LLM": {
    "DefaultProvider": "DeepSeek",
    "DefaultTemparature": "0.2",
    "DefaultMaxTokens": "4096",
    "OpenAI": {
      "ApiKey": "your-openai-api-key",
      "Model": "gpt-3.5-turbo"
    },
    "DeepSeek": {
      "ApiKey": "your-deepseek-api-key",
      "Model": "deepseek-chat"
    },
    "Gemini": {
      "ApiKey": "your-gemini-api-key",
      "Model": "gemini-1.5-flash"
    }
  },
  "Conversation": {
    "DefaultMaxMessages": "30"
  }
}
```

### 2. Get API Keys

- **OpenAI**: [OpenAI Platform](https://platform.openai.com/)
- **DeepSeek**: [DeepSeek Platform](https://platform.deepseek.com/)
- **Gemini**: [Google AI Studio](https://aistudio.google.com/)

### 3. Server Configuration

Ensure your MCP Server is running and accessible at the configured host and port. The default configuration expects:
- **Host**: `localhost`
- **Port**: `8080`

## Usage

### Running the Application

```bash
dotnet run
```

### Application Flow

1. **Configuration Loading**: Loads settings from `appsettings.json`
2. **Server Health Check**: Verifies MCP Server connectivity
3. **Tool Discovery**: Discovers available database tools
4. **LLM Setup**: Initializes the configured LLM provider
5. **Interactive Mode**: Ready for natural language queries

### Interactive Console

Once started, you can interact with your database using natural language:

```
🤖 LLM Orchestrator ready! Type 'exit' to quit.
❓ Question: What tables exist in the database?
❓ Question: Show me the structure of the Users table
❓ Question: exit
👋 Goodbye!
```

## Configuration Options

### Server Settings
- `Host`: MCP Server hostname/IP
- `Port`: MCP Server port
- `RequestTimeoutSeconds`: HTTP request timeout
- `MaxRequestSizeMB`: Maximum request size limit

### LLM Settings
- `DefaultProvider`: Primary LLM provider (OpenAI, DeepSeek, or Gemini)
- `DefaultTemperature`: AI response creativity (0.0-1.0)
- `DefaultMaxTokens`: Maximum response length
- Provider-specific API keys and model names

### Conversation Settings
- `DefaultMaxMessages`: Maximum conversation history length

## Security

- **Never commit API keys** to version control
- `appsettings.json` is excluded via `.gitignore`
- Use `appsettings.template.json` as a reference
- Keep your API keys secure and rotate them regularly

## Troubleshooting

### Common Issues

1. **"Server is not responding"**
   - Verify MCP Server is running
   - Check host/port configuration
   - Ensure firewall allows connections

2. **"API key not configured"**
   - Verify API key is set in `appsettings.json`
   - Check API key format and validity
   - Ensure sufficient API quota

3. **"Failed to discover tools"**
   - Verify server health
   - Check server tool configuration
   - Review server logs for errors

## Architecture

- **AppConfig**: Centralized configuration management
- **ClientConfig**: Server and client settings
- **HealthChecker**: Server connectivity monitoring
- **ToolDiscoverer**: Dynamic tool discovery from MCP Server
- **DynamicToolExecutor**: Tool execution engine
- **LLMOrchestrator**: AI provider integration and query processing

## Contributing

1. Follow existing code patterns
2. Update configuration templates when adding new settings
3. Maintain security best practices for sensitive data
4. Test with multiple LLM providers
