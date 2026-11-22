using ClaimsAssistant.Agents;
using Xunit;

namespace ClaimsAssistant.Tests;

public class GuardrailsTests
{
    private static readonly GuardrailOptions Options = new GuardrailOptions();

    private static IntakeFacts Facts(decimal amount = 18_500m)
    {
        return new IntakeFacts
        {
            ClaimId = "CLM-2001",
            PolicyId = "POL-1001",
            Peril = "wind",
            EstimatedAmount = amount,
            DamageSummary = "Roof damage."
        };
    }

    private static CoverageAssessment Coverage(
        string decision = "covered", bool withinLimit = true, int citations = 1)
    {
        var cited = new ClauseCitation[citations];
        for (var i = 0; i < citations; i++)
            cited[i] = new ClauseCitation { ClauseId = "C-" + (i + 1), Why = "Relevant." };

        return new CoverageAssessment
        {
            ClaimId = "CLM-2001",
            Decision = decision,
            WithinCoverageLimit = withinLimit,
            Confidence = 0.9,
            Citations = cited
        };
    }

    private static TriageOutcome Outcome(
        string decision = "covered", double confidence = 0.9, bool review = false)
    {
        return new TriageOutcome
        {
            ClaimId = "CLM-2001",
            Decision = decision,
            Summary = "Summary.",
            Confidence = confidence,
            NeedsHumanReview = review,
            Reasons = new[] { "Covered under C-1" }
        };
    }

    private static GuardrailResult Run(
        IntakeFacts? facts = null,
        CoverageAssessment? coverage = null,
        TriageOutcome? outcome = null)
    {
        return Guardrails.Apply(
            "CLM-2001",
            facts ?? Facts(),
            coverage ?? Coverage(),
            outcome ?? Outcome(),
            Options);
    }

    [Fact]
    public void CleanCoveredClaim_NeedsNoReview()
    {
        var result = Run();
        Assert.False(result.Outcome.NeedsHumanReview);
        Assert.Empty(result.Flags);
        Assert.Equal("covered", result.Outcome.Decision);
    }

    [Fact]
    public void LowConfidence_ForcesReview()
    {
        var result = Run(outcome: Outcome(confidence: 0.5));
        Assert.True(result.Outcome.NeedsHumanReview);
        Assert.NotEmpty(result.Flags);
    }

    [Fact]
    public void Denial_ForcesReview()
    {
        var result = Run(coverage: Coverage("not_covered"), outcome: Outcome("not_covered"));
        Assert.True(result.Outcome.NeedsHumanReview);
        Assert.Equal("not_covered", result.Outcome.Decision);
    }

    [Fact]
    public void HighValueClaim_ForcesReview()
    {
        var result = Run(facts: Facts(150_000m));
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void AmountOverCoverageLimit_ForcesReview()
    {
        var result = Run(coverage: Coverage(withinLimit: false));
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void DisagreeingDecisions_BecomeNeedsReview()
    {
        var result = Run(coverage: Coverage("covered"), outcome: Outcome("not_covered"));
        Assert.Equal("needs_review", result.Outcome.Decision);
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void InvalidDecision_BecomesNeedsReview()
    {
        var result = Run(outcome: Outcome("maybe"));
        Assert.Equal("needs_review", result.Outcome.Decision);
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void NoCitations_BecomesNeedsReview()
    {
        var result = Run(coverage: Coverage(citations: 0));
        Assert.Equal("needs_review", result.Outcome.Decision);
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void ConfidenceOutOfRange_ForcesReview()
    {
        var result = Run(outcome: Outcome(confidence: 1.7));
        Assert.True(result.Outcome.NeedsHumanReview);
        Assert.Equal(0, result.Outcome.Confidence);
    }

    [Fact]
    public void ClaimIdMismatch_IsFlaggedAndCorrected()
    {
        var result = Run(facts: Facts() with { ClaimId = "CLM-9999" });
        Assert.True(result.Outcome.NeedsHumanReview);
        Assert.Equal("CLM-2001", result.Outcome.ClaimId);
    }

    [Fact]
    public void MissingSummary_GetsPlaceholderAndReview()
    {
        var result = Run(outcome: Outcome() with { Summary = "" });
        Assert.False(string.IsNullOrWhiteSpace(result.Outcome.Summary));
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void Guardrails_NeverRemoveAnExistingReviewFlag()
    {
        var result = Run(outcome: Outcome(review: true));
        Assert.True(result.Outcome.NeedsHumanReview);
    }

    [Fact]
    public void GuardrailFlags_AreAddedToReasons()
    {
        var result = Run(outcome: Outcome(confidence: 0.4));
        Assert.Contains(result.Outcome.Reasons, r => r.StartsWith("Guardrail:"));
    }
}
