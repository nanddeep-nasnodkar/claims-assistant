namespace ClaimsAssistant.Agents;

public sealed class GuardrailOptions
{
    public double ConfidenceThreshold { get; init; } = 0.7;
    public decimal HighValueThreshold { get; init; } = 100_000m;
}

public sealed record GuardrailResult(TriageOutcome Outcome, IReadOnlyList<string> Flags);

public sealed class ClaimNotFoundException(string claimId)
    : Exception($"Claim '{claimId}' was not found.")
{
    public string ClaimId { get; } = claimId;
}

public sealed record TriageReport
{
    public required TriageOutcome Outcome { get; init; }
    public required string SavedRecordId { get; init; }
    public required IReadOnlyList<string> GuardrailFlags { get; init; }
    public required string PolicyId { get; init; }
    public required decimal EstimatedAmount { get; init; }
}

/// <summary>
/// Deterministic checks applied to the agents' output before anything is saved.
/// Guardrails can only make the outcome safer: they add review flags, they never remove one.
/// </summary>
public static class Guardrails
{
    public static readonly string[] ValidDecisions =
        ["covered", "partially_covered", "not_covered", "needs_review"];

    public static GuardrailResult Apply(
        string requestedClaimId,
        IntakeFacts facts,
        CoverageAssessment coverage,
        TriageOutcome outcome,
        GuardrailOptions options)
    {
        var flags = new List<string>();
        var review = outcome.NeedsHumanReview;
        var decision = outcome.Decision;
        var confidence = outcome.Confidence;
        var summary = outcome.Summary;

        void Flag(string text)
        {
            flags.Add(text);
            review = true;
        }

        if (!Same(facts.ClaimId, requestedClaimId) || !Same(outcome.ClaimId, requestedClaimId))
            Flag("A claim ID returned by an agent did not match the request");

        if (!ValidDecisions.Contains(decision))
        {
            decision = "needs_review";
            Flag("Invalid decision value");
        }

        if (decision != coverage.Decision)
        {
            decision = "needs_review";
            Flag("Triage and Coverage decisions disagree");
        }

        if (double.IsNaN(confidence) || confidence < 0 || confidence > 1)
        {
            confidence = 0;
            Flag("Confidence out of range");
        }
        else if (confidence < options.ConfidenceThreshold)
        {
            Flag($"Confidence {confidence:0.00} is below {options.ConfidenceThreshold:0.00}");
        }

        if (decision == "not_covered")
            Flag("Denials always need human review");

        if (facts.EstimatedAmount >= options.HighValueThreshold)
            Flag($"High-value claim (estimated amount {facts.EstimatedAmount:N0})");

        if (!coverage.WithinCoverageLimit)
            Flag("Estimated amount exceeds the coverage limit");

        if (decision != "needs_review" && coverage.Citations.Length == 0)
        {
            decision = "needs_review";
            Flag("No policy clause was cited");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "No summary was generated. See reasons.";
            Flag("Missing summary");
        }

        var final = outcome with
        {
            ClaimId = requestedClaimId,
            Decision = decision,
            Summary = summary,
            Confidence = confidence,
            NeedsHumanReview = review || decision == "needs_review",
            Reasons = outcome.Reasons.Concat(flags.Select(f => "Guardrail: " + f)).ToArray()
        };
        return new GuardrailResult(final, flags);
    }

    private static bool Same(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
