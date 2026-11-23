using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using ClaimsAssistant.McpServer;
using ClaimsAssistant.Shared;
using Microsoft.Azure.Cosmos;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

string Need(string key) =>
    config[key] ?? throw new InvalidOperationException($"Missing configuration value: {key}");

var databaseName = config["Cosmos:Database"] ?? "claims";

// Uses your az login locally and the managed identity once deployed (Day 5).
var credential = new DefaultAzureCredential();

var cosmos = new CosmosClient(Need("Cosmos:Endpoint"), credential, new CosmosClientOptions
{
    ApplicationName = "claims-assistant-mcp",
    SerializerOptions = new CosmosSerializationOptions
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    }
});

var embeddingClient = new AzureOpenAIClient(new Uri(Need("OpenAI:Endpoint")), credential)
    .GetEmbeddingClient(config["OpenAI:EmbeddingDeployment"] ?? "text-embedding-3-small");

IEmbedder embedder = new AzureOpenAIEmbedder(embeddingClient);

// dotnet run --project src/ClaimsAssistant.McpServer -- seed
if (args.Contains("seed"))
{
    await Seeder.RunAsync(cosmos, databaseName, embedder, FindDataDir());
    return;
}

builder.Services.AddSingleton(cosmos);
builder.Services.AddSingleton<IClaimsStore>(new CosmosClaimsStore(cosmos, databaseName));
builder.Services.AddSingleton(embedder);

// Tracing to Application Insights, only when a connection string is configured.
if (!string.IsNullOrWhiteSpace(config["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
    builder.Services.ConfigureOpenTelemetryTracerProvider(
        (sp, tracing) => tracing.AddSource(McpTelemetry.SourceName));
}

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly();

var app = builder.Build();
app.MapGet("/health", () => "ok");
app.MapMcp("/mcp");
app.Run();

// dotnet run starts the app in the project folder, so look for data\policies.json there
// and in each parent folder until it is found (the repo root).
static string FindDataDir()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "data");
        if (File.Exists(Path.Combine(candidate, "policies.json"))) return candidate;
        dir = dir.Parent;
    }
    throw new DirectoryNotFoundException("No data folder with policies.json found above " +
        Directory.GetCurrentDirectory());
}
