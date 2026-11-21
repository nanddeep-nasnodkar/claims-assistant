using System.ComponentModel;
using ClaimsAssistant.Shared;
using ModelContextProtocol;
using ModelContextProtocol.Server;
namespace ClaimsAssistant.McpServer;
[McpServerToolType]
public sealed class ClaimsTools(IClaimsStore store, IEmbedder embedder)
{
    private static readonly HashSet<string> Decisions =
        ["covered", "partially_covered", "not_covered", "needs_review"];
    [McpServerTool(Name = "get_policy"),
     Description("Returns a policy's limits and deductibles for a policy ID such as POL-1001.")]
    public async Task<Policy> GetPolicy(
        [Description("Policy ID, for example POL-1001")] string policyId,
        CancellationToken ct)
    {    
        var id = Require(policyId, nameof(policyId));
        return await store.GetPolicyAsync(id, ct)
            ?? throw new McpException($"Policy '{id}' was not found.");
    }

    [McpServerTool(Name = "get_claim"),
     Description("Returns a claim (peril, description, amount) for a claim ID such as CLM-2001.")]
    public async Task<Claim> GetClaim(
        [Description("Claim ID, for example CLM-2001")] string claimId,
        CancellationToken ct)
    {
        var id = Require(claimId, nameof(claimId));
        return await store.GetClaimAsync(id, ct)
            ?? throw new McpException($"Claim '{id}' was not found.");
    }

    [McpServerTool(Name = "search_policy_clauses"),
     Description("Finds the policy clauses most relevant to a question, using semantic search.")]
    public async Task<IReadOnlyList<ClauseMatch>> SearchPolicyClauses(
        [Description("Policy ID, for example POL-1001")] string policyId,
        [Description("What to look for, e.g. 'water damage from heavy rain'")] string query,
        [Description("Number of clauses to return, 1 to 5 (default 3)")] int topK = 3,
        CancellationToken ct = default)
    {
        var id = Require(policyId, nameof(policyId));
        var text = Require(query, nameof(query));
        var embedding = await embedder.EmbedAsync(text, ct);
        return await store.SearchClausesAsync(id, embedding, Math.Clamp(topK, 1, 5), ct);

        // Console.WriteLine("embedding...");
        // var embedding = await embedder.EmbedAsync(text, ct);
        // Console.WriteLine($"embedded: {embedding.Length} dims");
        // var results = await store.SearchClausesAsync(id, embedding, Math.Clamp(topK, 1, 5), ct);
        // Console.WriteLine($"searched: {results.Count} results");
        // return results;
    }

    [McpServerTool(Name = "save_triage_result"),
     Description("Saves the triage outcome for a claim and returns the ID of the saved record.")]
    public async Task<string> SaveTriageResult(
        [Description("Claim ID, for example CLM-2001")] string claimId,
        [Description("One of: covered, partially_covered, not_covered, needs_review")] string decision,
        [Description("Two or three sentence summary for the adjuster")] string summary,
        [Description("True if a person should review this claim")] bool needsHumanReview,
        [Description("Confidence from 0.0 to 1.0")] double confidence,
        [Description("Short reasons, citing clause IDs where relevant")] string[] reasons,
        CancellationToken ct)
    {
    
        var id = Require(claimId, nameof(claimId));
        if (!Decisions.Contains(decision))
            throw new McpException("decision must be one of: " + string.Join(", ", Decisions));
        if (confidence is < 0 or > 1)
            throw new McpException("confidence must be between 0 and 1.");
        var result = new TriageResult
        {
            Id = Guid.NewGuid().ToString("N"),
            ClaimId = id,
            Decision = decision,
            Summary = Require(summary, nameof(summary)),
            // Low-confidence results always go to a person, whatever the agent says.
            NeedsHumanReview = needsHumanReview || confidence < 0.7,
            Confidence = confidence,
            Reasons = reasons ?? [],
            CreatedAtUtc = DateTime.UtcNow.ToString("O")
        };
        return await store.SaveTriageResultAsync(result, ct);
    }
    private static string Require(string? value, string name) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new McpException($"{name} is required.")
            : value.Trim();
    }