using System.ComponentModel;
namespace ClaimsAssistant.Agents;
public sealed record IntakeFacts
{
    [Description("Claim ID, for example CLM-2001")]
    public string ClaimId { get; init; } = "";
    [Description("Policy ID the claim is filed under")]
    public string PolicyId { get; init; } = "";
    [Description("Cause of loss: wind, hail, fire, water or flood")]
    public string Peril { get; init; } = "";
    [Description("Date of loss in ISO format")]
    public string DateOfLoss { get; init; } = "";
    public decimal EstimatedAmount { get; init; }
    [Description("One or two plain sentences describing the damage")]
    public string DamageSummary { get; init; } = "";
    [Description("Short factual statements relevant to coverage")]
    public string[] KeyFacts { get; init; } = [];
}
public sealed record ClauseCitation
{
    [Description("Clause ID, for example C-FLOOD")]
    public string ClauseId { get; init; } = "";
    [Description("One sentence on why this clause matters for the claim")]
    public string Why { get; init; } = "";
}
public sealed record CoverageAssessment
{
    public string ClaimId { get; init; } = "";
    [Description("One of: covered, partially_covered, not_covered, needs_review")]
    public string Decision { get; init; } = "";
    [Description("Clauses relied on for the decision")]
    public ClauseCitation[] Citations { get; init; } = [];
    [Description("True if the estimated amount is within the coverage limit")]
    public bool WithinCoverageLimit { get; init; }
    [Description("Which deductible applies and how, or empty if unknown")]
    public string DeductibleNote { get; init; } = "";
    [Description("Confidence from 0.0 to 1.0")]
    public double Confidence { get; init; }
    [Description("Anything uncertain or missing")]
    public string[] OpenQuestions { get; init; } = [];
}
public sealed record TriageOutcome
{
    public string ClaimId { get; init; } = "";
    [Description("One of: covered, partially_covered, not_covered, needs_review")]
    public string Decision { get; init; } = "";
    [Description("Two or three sentence summary for a claims adjuster")]
    public string Summary { get; init; } = "";
    public bool NeedsHumanReview { get; init; }
    [Description("Confidence from 0.0 to 1.0")]
    public double Confidence { get; init; }
     [Description("Short reasons that cite clause IDs")]
    public string[] Reasons { get; init; } = [];
    [Description("The ID returned by the save_triage_result tool")]
    public string SavedRecordId { get; init; } = "";
}