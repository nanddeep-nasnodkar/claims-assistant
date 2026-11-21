using System.Text.Json;
using ClaimsAssistant.Shared;
using Microsoft.Azure.Cosmos;
namespace ClaimsAssistant.McpServer;
public static class Seeder
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static async Task RunAsync(
        CosmosClient cosmos, string databaseName, IEmbedder embedder, string dataDir)
    {
        var db = cosmos.GetDatabase(databaseName);
        foreach (var p in Load<Policy>(dataDir, "policies.json"))
            await db.GetContainer("policies").UpsertItemAsync(p, new PartitionKey(p.Id));
        foreach (var c in Load<Claim>(dataDir, "claims.json"))
            await db.GetContainer("claims").UpsertItemAsync(c, new PartitionKey(c.PolicyId));
        foreach (var c in Load<Clause>(dataDir, "clauses.json"))
        {
            var embedding = await embedder.EmbedAsync($"{c.Title}. {c.Text}", CancellationToken.None);
            var doc = c with { Id = $"{c.PolicyId}-{c.ClauseId}", Embedding = embedding };
            await db.GetContainer("policyClauses").UpsertItemAsync(doc, new PartitionKey(c.PolicyId));
        }
        Console.WriteLine("Seed complete.");
    }
    private static List<T> Load<T>(string dir, string file) =>
        JsonSerializer.Deserialize<List<T>>(File.ReadAllText(Path.Combine(dir, file)), Json) ?? [];
    }