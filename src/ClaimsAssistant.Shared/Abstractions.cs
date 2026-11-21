namespace ClaimsAssistant.Shared;
public interface IClaimsStore
{
    Task<Policy?> GetPolicyAsync(string policyId, CancellationToken ct);
    Task<Claim?> GetClaimAsync(string claimId, CancellationToken ct);
    Task<IReadOnlyList<ClauseMatch>> SearchClausesAsync(
        string policyId, float[] embedding, int topK, CancellationToken ct);
    Task<string> SaveTriageResultAsync(TriageResult result, CancellationToken ct);
}
public interface IEmbedder
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
