using System.Text.Json;
using ClaimsAssistant.Agents;
using Microsoft.Extensions.Configuration;
var config = new ConfigurationBuilder()
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables()
    .Build();
string Need(string key) =>
    config[key] ?? throw new InvalidOperationException($"Missing configuration value: {key}");
// dotnet run --project src\ClaimsAssistant.Harness -- CLM-2001 CLM-2002
var claimIds = args.Length > 0 ? args : ["CLM-2001", "CLM-2002", "CLM-2003"];
await using var pipeline = await ClaimsTriagePipeline.CreateAsync(new PipelineOptions
{
    OpenAIEndpoint = new Uri(Need("OpenAI:Endpoint")),
    ChatDeployment = Need("OpenAI:ChatDeployment"),
    McpEndpoint = new Uri(config["Mcp:Endpoint"] ?? "http://localhost:5100/mcp")
});
var pretty = new JsonSerializerOptions { WriteIndented = true };
foreach (var id in claimIds)
{
    Console.WriteLine($"=== {id} ===");
    var started = DateTime.UtcNow;
    try
    {
        var outcome = await pipeline.TriageAsync(id);
        Console.WriteLine(JsonSerializer.Serialize(outcome, pretty));
    }        
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: {ex.Message}");
    }
    Console.WriteLine($"({(DateTime.UtcNow - started).TotalSeconds:F0}s)\n");
}