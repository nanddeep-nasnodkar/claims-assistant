using System.Diagnostics;

namespace ClaimsAssistant.McpServer;

public static class McpTelemetry
{
    public const string SourceName = "ClaimsAssistant.Mcp";

    public static readonly ActivitySource Source = new(SourceName);
}
