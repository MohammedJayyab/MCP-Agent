using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MCPServer;

public class Program
{
    private static void DisplayHeader()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║            MCP Server Manager              ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    private static void DisplayStatus(string message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ResetColor();
    }

    private static IConfiguration BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();
    }

    public static async Task Main(string[] args)
    {
        DisplayHeader();

        var configuration = BuildConfiguration();
        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? 
                              throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var baseUrl = configuration.GetValue<string>("ServerSettings:BaseUrl") ?? 
                     "http://localhost:8080/";

        ISqlConnectionFactory connectionFactory = new SqlConnectionFactory(connectionString);
        IDatabaseService databaseService = new DatabaseService(connectionFactory);

        var server = new Server(baseUrl, databaseService);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            DisplayStatus("\nShutting down server...", ConsoleColor.Yellow);
            cts.Cancel();
            server.Stop();
        };

        try
        {
            DisplayStatus("Starting MCP Server...", ConsoleColor.Green);
            DisplayStatus($"Server URL: {baseUrl}", ConsoleColor.White);
            DisplayStatus("Press Ctrl+C to stop the server.", ConsoleColor.White);
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            DisplayStatus("Server stopped gracefully.", ConsoleColor.DarkGreen);
        }
        catch (Exception ex)
        {
            DisplayStatus($"Server error: {ex.Message}", ConsoleColor.Red);
        }
    }
}