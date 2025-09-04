using MCPServer;
using Microsoft.Extensions.Configuration;

public class Program
{
    private static void DisplayHeader()
    {
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║            Optimized MCP Server            ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        Console.ResetColor();
    }

    public static async Task Main(string[] args)
    {
        DisplayHeader();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection") ??
                              throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        var baseUrl = configuration.GetValue<string>("ServerSettings:BaseUrl") ?? "http://localhost:8080/";

        // Use consolidated database service with built-in connection pooling
        var databaseService = new DatabaseService(connectionString);
        var server = new Server(baseUrl, databaseService);

        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n🛑 Shutting down server...");
            cts.Cancel();
            server.Stop();
            databaseService.Dispose();
        };

        try
        {
            Console.WriteLine("🚀 Starting Optimized MCP Server...");
            Console.WriteLine($"🌐 Server URL: {baseUrl}");
            Console.WriteLine("📝 Press Ctrl+C to stop the server.");
            
            await server.StartAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("✅ Server stopped gracefully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Server error: {ex.Message}");
        }
        finally
        {
            databaseService.Dispose();
        }
    }
}