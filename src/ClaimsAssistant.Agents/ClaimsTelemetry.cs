using System.Diagnostics;

namespace ClaimsAssistant.Agents;

public static class ClaimsTelemetry
{
    public const string SourceName = "ClaimsAssistant";

    public static readonly ActivitySource Source = new(SourceName);
}
