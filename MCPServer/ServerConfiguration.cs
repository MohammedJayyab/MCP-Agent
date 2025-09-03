namespace MCPServer;

public class ServerConfiguration
{
    public ConnectionStrings ConnectionStrings { get; set; } = new();
    public ServerSettings ServerSettings { get; set; } = new();
}

public class ConnectionStrings
{
    public string DefaultConnection { get; set; } = string.Empty;
}

public class ServerSettings
{
    public string BaseUrl { get; set; } = string.Empty;
}
