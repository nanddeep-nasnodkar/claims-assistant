using System.Net;
using ClaimsAssistant.Shared;
using Microsoft.Azure.Cosmos;
namespace ClaimsAssistant.McpServer;
public sealed class CosmosClaimsStore : IClaimsStore
{
    private readonly Container _policies;
    private readonly Container _claims;
    private readonly Container _clauses;
    private readonly Container _results;
    public CosmosClaimsStore(CosmosClient client, string databaseName)
    {
        var db = client.GetDatabase(databaseName);
        _policies = db.GetContainer("policies");
        _claims = db.GetContainer("claims");
        _clauses = db.GetContainer("policyClauses");
        _results = db.GetContainer("triageResults");
    }
    public async Task<Policy?> GetPolicyAsync(string policyId, CancellationToken ct)
    {
        try
        {
        
            var response = await _policies.ReadItemAsync<Policy>(
                policyId, new PartitionKey(policyId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }
    // Claims are partitioned by policyId, so a lookup by claim ID alone is a cross-partition
    // query. That is fine for a small demo data set.
    public async Task<Claim?> GetClaimAsync(string claimId, CancellationToken ct)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @id")
            .WithParameter("@id", claimId);
        using var iterator = _claims.GetItemQueryIterator<Claim>(query);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            var hit = page.FirstOrDefault();
            if (hit is not null) return hit;
        }
        return null;
    }
    // public async Task<IReadOnlyList<ClauseMatch>> SearchClausesAsync(
    //     string policyId, float[] embedding, int topK, CancellationToken ct)
    // {
    //     // topK is a clamped int, so it is safe to place directly in the query text.
    //     // With cosine distance, a higher score means a closer match.
    //     var query = new QueryDefinition(
    //             $"SELECT TOP {topK} c.clauseId, c.title, c.text, " +
    //             "VectorDistance(c.embedding, @embedding) AS score " +
    //              "FROM c WHERE c.policyId = @policyId " +
    //             "ORDER BY VectorDistance(c.embedding, @embedding)")
    //         .WithParameter("@embedding", embedding)
    //         .WithParameter("@policyId", policyId);
    //     var options = new QueryRequestOptions { PartitionKey = new PartitionKey(policyId) };
    //     var matches = new List<ClauseMatch>();
    //     using var iterator = _clauses.GetItemQueryIterator<ClauseMatch>(query, requestOptions: options);
    //     while (iterator.HasMoreResults)
    //     {
    //     }
    //         var page = await iterator.ReadNextAsync(ct);
    //         matches.AddRange(page);
    //     return matches;
    // }

public async Task<IReadOnlyList<ClauseMatch>> SearchClausesAsync(
    string policyId, float[] embedding, int topK, CancellationToken ct)
{
    var query = new QueryDefinition(
            $"SELECT TOP {topK} c.clauseId, c.title, c.text, " +
            "VectorDistance(c.embedding, @embedding) AS score " +
            "FROM c WHERE c.policyId = @policyId " +
            "ORDER BY VectorDistance(c.embedding, @embedding)")
        .WithParameter("@embedding", embedding)
        .WithParameter("@policyId", policyId);

    var options = new QueryRequestOptions { PartitionKey = new PartitionKey(policyId) };

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(15));

    var matches = new List<ClauseMatch>();
    using var iterator = _clauses.GetItemQueryIterator<ClauseMatch>(query, requestOptions: options);
    while (iterator.HasMoreResults)
    {
        var page = await iterator.ReadNextAsync(cts.Token);
        matches.AddRange(page);
    }
    return matches;
}

    public async Task<string> SaveTriageResultAsync(TriageResult result, CancellationToken ct)
    {
        await _results.UpsertItemAsync(result, new PartitionKey(result.ClaimId), cancellationToken: ct);
        return result.Id;
    }
}