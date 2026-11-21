using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
namespace ClaimsAssistant.Agents;
public sealed class PipelineOptions
{
    public required Uri OpenAIEndpoint { get; init; }
    public required string ChatDeployment { get; init; }
    public Uri McpEndpoint { get; init; } = new("http://localhost:5100/mcp");
}
public sealed class ClaimsTriagePipeline : IAsyncDisposable
{
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };
    private readonly McpClient _mcp;
    private readonly AIAgent _intake;
    private readonly AIAgent _coverage;
    private readonly AIAgent _triage;
    private ClaimsTriagePipeline(McpClient mcp, AIAgent intake, AIAgent coverage, AIAgent triage)
    {
        _mcp = mcp;
        _intake = intake;
        _coverage = coverage;
        _triage = triage;
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

    IChatClient chat = new AzureOpenAIClient(options.OpenAIEndpoint, new DefaultAzureCredential())
    .GetChatClient(options.ChatDeployment)
    .AsIChatClient();

    AIAgent intake = chat.AsAIAgent(
        name: "Intake", instructions: Prompts.Intake, tools: Pick("get_claim"));
    AIAgent coverage = chat.AsAIAgent(
        name: "Coverage", instructions: Prompts.Coverage,
        tools: Pick("get_policy", "search_policy_clauses"));
    AIAgent triage = chat.AsAIAgent(
        name: "Triage", instructions: Prompts.Triage, tools: Pick("save_triage_result"));

    return new ClaimsTriagePipeline(mcp, intake, coverage, triage);
}
    public async Task<TriageOutcome> TriageAsync(string claimId)
    {
        var intake = await _intake.RunAsync<IntakeFacts>(
            $"Extract the facts for claim {claimId}.");
            var coverage = await _coverage.RunAsync<CoverageAssessment>(
            "Assess coverage for this claim.\nClaim facts:\n" + Show(intake.Result));
        var triage = await _triage.RunAsync<TriageOutcome>(
            "Produce the triage outcome and save it.\n" +
            "Claim facts:\n" + Show(intake.Result) + "\n" +
            "Coverage assessment:\n" + Show(coverage.Result));
        var outcome = triage.Result;
        if (string.IsNullOrWhiteSpace(outcome.SavedRecordId))
            throw new InvalidOperationException(
                "The Triage agent did not save a result (SavedRecordId is empty).");
        return outcome;
    }
    private static string Show<T>(T value) => JsonSerializer.Serialize(value, Pretty);
    public ValueTask DisposeAsync() => _mcp.DisposeAsync();
}
