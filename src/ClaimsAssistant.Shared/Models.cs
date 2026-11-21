namespace ClaimsAssistant.Shared;
public sealed record Policy
{
    public string Id { get; init; } = "";
    public string HolderName { get; init; } = "";
    public string State { get; init; } = "";
    public decimal CoverageLimit { get; init; }
    public decimal Deductible { get; init; }
    public decimal WindHailDeductiblePercent { get; init; }
}
public sealed record Claim
{
    public string Id { get; init; } = "";
    public string PolicyId { get; init; } = "";
    public string DateOfLoss { get; init; } = "";   // ISO date, e.g. 2026-08-14
    public string Peril { get; init; } = "";        // wind, hail, fire, water, flood
    public string Description { get; init; } = "";
    public decimal EstimatedAmount { get; init; }
}
public sealed record Clause
{
    public string Id { get; init; } = "";
    public string PolicyId { get; init; } = "";
    public string ClauseId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public float[]? Embedding { get; init; }
}
public sealed record ClauseMatch
{
    public string ClauseId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public double Score { get; init; }
}
public sealed record TriageResult
{
    public string Id { get; init; } = "";
    public string ClaimId { get; init; } = "";
    public string Decision { get; init; } = "";
    public string Summary { get; init; } = "";
    public bool NeedsHumanReview { get; init; }
    public double Confidence { get; init; }
    public string[] Reasons { get; init; } = [];
    public string CreatedAtUtc { get; init; } = "";
}