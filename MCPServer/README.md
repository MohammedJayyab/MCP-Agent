# MCP Server

A .NET-based Model Context Protocol (MCP) server that provides database access and management capabilities through JSON-RPC endpoints. The server enables secure SQL execution, schema exploration, and health monitoring for database operations.

## Features

- **JSON-RPC Server**: HTTP-based RPC server with CORS support
- **Database Integration**: SQL Server connectivity with parameterized queries
- **Tool Discovery**: Dynamic tool registration and discovery system
- **Health Monitoring**: Server status and connectivity checks
- **Security**: Request size limits and SQL injection protection
- **Configurable**: Easy setup through configuration files

## Architecture

### Core Components

- **Server**: HTTP listener and request processor
- **DatabaseService**: SQL execution and schema management
- **SqlConnectionFactory**: Database connection management
- **Tool System**: Extensible tool registration and execution

### Available Tools

The server provides several built-in tools for database operations:

| Tool | Description | Parameters |
|------|-------------|------------|
| `health` | Server health check | None |
| `executeSQL` | Execute SQL queries | `query` (string) |
| `getTableSchema` | Get table structure | `tableName` (string) |
| `getDatabaseSchema` | Get full database schema | None |
| `getTools` | List available tools | None |

## Prerequisites

- .NET 8.0 or later
- SQL Server instance (local or remote)
- Database access credentials
- Network access to the target database

## Setup

### 1. Configuration Setup

**Copy the template configuration file:**
```bash
cp appsettings.template.json appsettings.json
```

**Update `appsettings.json` with your database settings:**

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER\\INSTANCE;Database=YOUR_DATABASE;User ID=YOUR_USERNAME;Password=YOUR_PASSWORD;TrustServerCertificate=True;MultipleActiveResultSets=true"
  },
  "ServerSettings": {
    "BaseUrl": "http://localhost:8080/"
  }
}
```

### 2. Database Connection

**Connection String Parameters:**
- `Server`: SQL Server instance (e.g., `localhost\\SQLEXPRESS`)
- `Database`: Target database name
- `User ID`: Database username
- `Password`: Database password
- `TrustServerCertificate`: Set to `True` for development
- `MultipleActiveResultSets`: Enable concurrent connections

**Security Notes:**
- Use Windows Authentication when possible
- Store credentials securely (environment variables recommended for production)
- Enable SSL/TLS in production environments

### 3. Server Configuration

**Default Settings:**
- **Host**: `localhost`
- **Port**: `8080`
- **Protocol**: HTTP
- **Request Limit**: 1MB per request

## Usage

### Running the Server

```bash
dotnet run
```

**Expected Output:**
```
╔════════════════════════════════════════════╗
║            MCP Server Manager              ║
╚════════════════════════════════════════════╝
Starting MCP Server...
Server URL: http://localhost:8080/
Press Ctrl+C to stop the server.
RPC Server is listening on http://localhost:8080/
```

### API Endpoints

The server exposes a single JSON-RPC endpoint at the configured URL.

**Request Format:**
```json
{
  "jsonrpc": "2.0",
  "id": "request-id",
  "method": "toolName",
  "params": {
    "param1": "value1"
  }
}
```

**Response Format:**
```json
{
  "jsonrpc": "2.0",
  "id": "request-id",
  "result": "tool result",
  "error": null
}
```

### Example Requests

#### Health Check
```bash
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "health-1",
    "method": "health",
    "params": {}
  }'
```

#### Execute SQL Query
```bash
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "sql-1",
    "method": "executeSQL",
    "params": {
      "query": "SELECT TOP 5 * FROM Users"
    }
  }'
```

#### Get Table Schema
```bash
curl -X POST http://localhost:8080/ \
  -H "Content-Type: application/json" \
  -d '{
    "jsonrpc": "2.0",
    "id": "schema-1",
    "method": "getTableSchema",
    "params": {
      "tableName": "Users"
    }
  }'
```

## Configuration Options

### Server Settings
- `BaseUrl`: Server listening address and port
- `ConnectionStrings`: Database connection configuration

### Environment Variables
- `ASPNETCORE_ENVIRONMENT`: Set to `Development`, `Production`, etc.
- Database credentials can be overridden via environment variables

## Security Considerations

### Request Validation
- **Size Limits**: Maximum 1MB per request
- **Method Validation**: Only POST requests accepted
- **Content Type**: JSON-RPC format required

### Database Security
- **Parameterized Queries**: Prevents SQL injection
- **Connection Management**: Proper disposal of database connections
- **Error Handling**: Limited error information exposure

### Network Security
- **CORS Support**: Configurable cross-origin access
- **Firewall**: Ensure only authorized clients can access the server
- **HTTPS**: Use reverse proxy for production deployments

## Troubleshooting

### Common Issues

1. **"Connection string not found"**
   - Verify `appsettings.json` exists and is properly formatted
   - Check connection string syntax
   - Ensure database server is accessible

2. **"Server error: Access denied"**
   - Verify database credentials
   - Check user permissions
   - Ensure database exists and is accessible

3. **"Port already in use"**
   - Change port in configuration
   - Check for other services using the port
   - Restart the application

4. **"Database connection failed"**
   - Verify SQL Server is running
   - Check network connectivity
   - Validate connection string parameters

### Logging

The server provides console output for:
- Startup and shutdown events
- Request processing
- Database connection status
- Error conditions

## Development

### Adding New Tools

1. **Extend the tools.json file:**
```json
{
  "name": "newTool",
  "description": "Tool description",
  "parameters": {
    "param1": {
      "type": "string",
      "description": "Parameter description"
    }
  }
}
```

2. **Implement tool logic in DatabaseService**
3. **Add method handling in Server.cs**

### Project Structure

```
MCPServer/
├── Program.cs              # Application entry point
├── Server.cs               # HTTP server implementation
├── DatabaseService.cs      # Database operations
├── SqlConnectionFactory.cs # Connection management
├── tools.json             # Tool definitions
├── appsettings.json       # Configuration
└── ServerConfiguration.cs  # Configuration models
```

## Contributing

1. Follow existing code patterns and naming conventions
2. Add proper error handling and logging
3. Include parameter validation for new tools
4. Test with various SQL Server versions
5. Update documentation for new features

## License

This project is part of the MCP-Agent solution. Please refer to the main project license for usage terms.
