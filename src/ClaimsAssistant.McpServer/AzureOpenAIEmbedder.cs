using ClaimsAssistant.Shared;
using OpenAI.Embeddings;
namespace ClaimsAssistant.McpServer;
public sealed class AzureOpenAIEmbedder(EmbeddingClient client) : IEmbedder
{
    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var result = await client.GenerateEmbeddingAsync(text, cancellationToken: ct);
        return result.Value.ToFloats().ToArray();
    }
}