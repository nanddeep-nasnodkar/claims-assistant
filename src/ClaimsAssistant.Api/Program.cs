using System.Text.RegularExpressions;
using ClaimsAssistant.Agents;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

string Need(string key) =>
    config[key] ?? throw new InvalidOperationException($"Missing configuration value: {key}");

// Requests are tiny. A small limit stops people sending large text to the model.
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = 4096);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("triage", limiter =>
    {
        limiter.PermitLimit = 10;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });
});

// The MCP server must be running when the API starts.
var pipeline = await ClaimsTriagePipeline.CreateAsync(new PipelineOptions
{
    OpenAIEndpoint = new Uri(Need("OpenAI:Endpoint")),
    ChatDeployment = Need("OpenAI:ChatDeployment"),
    McpEndpoint = new Uri(config["Mcp:Endpoint"] ?? "http://localhost:5100/mcp"),
    GuardrailSettings = new GuardrailOptions
    {
        ConfidenceThreshold = config.GetValue("Guardrails:ConfidenceThreshold", 0.7),
        HighValueThreshold = config.GetValue("Guardrails:HighValueThreshold", 100_000m)
    }
});
builder.Services.AddSingleton(pipeline);

var app = builder.Build();
app.Lifetime.ApplicationStopping.Register(
    () => pipeline.DisposeAsync().AsTask().GetAwaiter().GetResult());

app.UseExceptionHandler();
app.UseRateLimiter();

// API docs only in Development, or when EnableApiDocs=true (for a demo).
if (app.Environment.IsDevelopment() || config.GetValue<bool>("EnableApiDocs"))
{
    app.MapOpenApi();            // /openapi/v1.json
    app.MapScalarApiReference(); // /scalar
}

var claimIdPattern = new Regex("^CLM-[0-9]{3,10}$", RegexOptions.CultureInvariant);

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/claims/triage", async (
        TriageRequest request, ClaimsTriagePipeline triage,
        ILogger<Program> logger, CancellationToken ct) =>
    {
        // Strict format: the ID goes into a model prompt, so free text is never accepted.
        var claimId = (request.ClaimId ?? "").Trim().ToUpperInvariant();
        if (!claimIdPattern.IsMatch(claimId))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["claimId"] = ["Use the format CLM-1234: CLM- followed by 3 to 10 digits."]
            });
        }

        var started = DateTime.UtcNow;
        try
        {
            var report = await triage.TriageAsync(claimId).WaitAsync(TimeSpan.FromSeconds(90), ct);
            logger.LogInformation(
                "Triaged {ClaimId}: decision={Decision} review={Review} flags={Flags} ({Seconds:F1}s)",
                claimId, report.Outcome.Decision, report.Outcome.NeedsHumanReview,
                report.GuardrailFlags.Count, (DateTime.UtcNow - started).TotalSeconds);
            return Results.Ok(report);
        }
        catch (ClaimNotFoundException)
        {
            return Results.Problem(
                title: "Claim not found", detail: $"No claim with ID {claimId}.", statusCode: 404);
        }
        catch (TimeoutException)
        {
            logger.LogWarning("Triage of {ClaimId} timed out", claimId);
            return Results.Problem(title: "Triage timed out", statusCode: 504);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Triage failed for {ClaimId}", claimId);
            return Results.Problem(
                title: "Triage failed", detail: "An upstream service failed. Try again later.",
                statusCode: 502);
        }
    })
    .RequireRateLimiting("triage")
    .WithName("TriageClaim")
    .WithSummary("Triage a claim with the three agents and save the result")
    .Produces<TriageReport>(StatusCodes.Status200OK)
    .ProducesValidationProblem()
    .ProducesProblem(StatusCodes.Status404NotFound)
    .ProducesProblem(StatusCodes.Status429TooManyRequests)
    .ProducesProblem(StatusCodes.Status502BadGateway)
    .ProducesProblem(StatusCodes.Status504GatewayTimeout);

app.Run();

public sealed record TriageRequest(string? ClaimId);
