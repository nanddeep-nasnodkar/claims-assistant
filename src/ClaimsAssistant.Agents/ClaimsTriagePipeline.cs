using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using OpenAI;

namespace ClaimsAssistant.Agents;

public sealed class PipelineOptions
{
    public required Uri OpenAIEndpoint { get; init; }
    public required string ChatDeployment { get; init; }
    public Uri McpEndpoint { get; init; } = new("http://localhost:5100/mcp");
    public GuardrailOptions GuardrailSettings { get; init; } = new();
}

public sealed class ClaimsTriagePipeline : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    private readonly McpClient _mcp;
    private readonly AIAgent _intake;
    private readonly AIAgent _coverage;
    private readonly AIAgent _triage;
    private readonly GuardrailOptions _guardrails;

    private ClaimsTriagePipeline(
        McpClient mcp, AIAgent intake, AIAgent coverage, AIAgent triage,
        GuardrailOptions guardrails)
    {
        _mcp = mcp;
        _intake = intake;
        _coverage = coverage;
        _triage = triage;
        _guardrails = guardrails;
    }

    public static async Task<ClaimsTriagePipeline> CreateAsync(
        PipelineOptions options, CancellationToken ct = default)
    {
        var mcp = await McpClient.CreateAsync(
            new HttpClientTransport(new HttpClientTransportOptions
            {
                Name = "claims-mcp",
                Endpoint = options.McpEndpoint,
                TransportMode = HttpTransportMode.StreamableHttp
            }),
            cancellationToken: ct);

        var allTools = await mcp.ListToolsAsync(cancellationToken: ct);

        // Least privilege: each agent only sees the tools it needs.
        List<AITool> Pick(params string[] names)
        {
            var picked = allTools.Where(t => names.Contains(t.Name)).Cast<AITool>().ToList();
            if (picked.Count != names.Length)
                throw new InvalidOperationException(
                    $"MCP server is missing a tool. Wanted: {string.Join(", ", names)}");
            return picked;
        }

        var credential = new DefaultAzureCredential();
        IChatClient chat = new AzureOpenAIClient(options.OpenAIEndpoint, credential)
            .GetChatClient(options.ChatDeployment)
            .AsIChatClient()
            .AsBuilder()
            .UseOpenTelemetry(sourceName: ClaimsTelemetry.SourceName)
            .Build();

        AIAgent intake = chat.AsAIAgent(
            name: "Intake", instructions: Prompts.Intake, tools: Pick("get_claim"));
        AIAgent coverage = chat.AsAIAgent(
            name: "Coverage", instructions: Prompts.Coverage,
            tools: Pick("get_policy", "search_policy_clauses"));

        // Day 4: the Triage agent has no tools. The application saves the result
        // itself, after the guardrails have run.
        AIAgent triage = chat.AsAIAgent(name: "Triage", instructions: Prompts.Triage);

        return new ClaimsTriagePipeline(mcp, intake, coverage, triage, options.GuardrailSettings);
    }

    public async Task<TriageReport> TriageAsync(string claimId)
    {
        using var root = ClaimsTelemetry.Source.StartActivity("triage");
        root?.SetTag("claim.id", claimId);

        IntakeFacts facts;
        using (var step = ClaimsTelemetry.Source.StartActivity("intake"))
        {
            facts = (await _intake.RunAsync<IntakeFacts>(
                $"Extract the facts for claim {claimId}.")).Result;
            step?.SetTag("claim.policy_id", facts.PolicyId);
        }

        if (facts.DamageSummary == "NOT FOUND" || string.IsNullOrWhiteSpace(facts.PolicyId))
            throw new ClaimNotFoundException(claimId);

        CoverageAssessment coverage;
        using (var step = ClaimsTelemetry.Source.StartActivity("coverage"))
        {
            coverage = (await _coverage.RunAsync<CoverageAssessment>(
                "Assess coverage for this claim.\nClaim facts:\n" + Show(facts))).Result;
            step?.SetTag("coverage.decision", coverage.Decision);
            step?.SetTag("coverage.confidence", coverage.Confidence);
        }

        TriageOutcome proposed;
        using (var step = ClaimsTelemetry.Source.StartActivity("triage-agent"))
        {
            proposed = (await _triage.RunAsync<TriageOutcome>(
                "Produce the triage outcome.\n" +
                "Claim facts:\n" + Show(facts) + "\n" +
                "Coverage assessment:\n" + Show(coverage))).Result;
            step?.SetTag("triage.decision", proposed.Decision);
        }

        GuardrailResult guarded;
        using (var step = ClaimsTelemetry.Source.StartActivity("guardrails"))
        {
            guarded = Guardrails.Apply(claimId, facts, coverage, proposed, _guardrails);
            step?.SetTag("guardrails.flag_count", guarded.Flags.Count);
            step?.SetTag("guardrails.needs_review", guarded.Outcome.NeedsHumanReview);
        }

        string savedId;
        using (var step = ClaimsTelemetry.Source.StartActivity("save"))
        {
            savedId = await SaveAsync(guarded.Outcome);
            step?.SetTag("saved.record_id", savedId);
        }

        root?.SetTag("triage.decision", guarded.Outcome.Decision);
        root?.SetTag("triage.needs_review", guarded.Outcome.NeedsHumanReview);

        return new TriageReport
        {
            Outcome = guarded.Outcome,
            SavedRecordId = savedId,
            GuardrailFlags = guarded.Flags,
            PolicyId = facts.PolicyId,
            EstimatedAmount = facts.EstimatedAmount
        };
    }

    private async Task<string> SaveAsync(TriageOutcome outcome)
    {
        var result = await _mcp.CallToolAsync("save_triage_result", new Dictionary<string, object?>
        {
            ["claimId"] = outcome.ClaimId,
            ["decision"] = outcome.Decision,
            ["summary"] = outcome.Summary,
            ["needsHumanReview"] = outcome.NeedsHumanReview,
            ["confidence"] = outcome.Confidence,
            ["reasons"] = outcome.Reasons
        });

        var text = result.Content.OfType<TextContentBlock>().FirstOrDefault()?.Text;
        if (result.IsError is true || string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "Saving the result failed: " + (text ?? "no response"));
        return text;
    }

    private static string Show<T>(T value) => JsonSerializer.Serialize(value, Pretty);

    public ValueTask DisposeAsync() => _mcp.DisposeAsync();
}
