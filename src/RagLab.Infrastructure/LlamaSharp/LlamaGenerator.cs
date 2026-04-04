using System.Text;
using LLama;
using LLama.Common;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.LlamaSharp;

public sealed class LlamaGenerator : IGenerator, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly StatelessExecutor _executor;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LlamaGenerator(LlamaSharpOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parameters = new ModelParams(options.GenerationModelPath)
        {
            ContextSize = (uint)options.ContextSize,
            GpuLayerCount = options.GpuLayerCount
        };

        _weights = LLamaWeights.LoadFromFile(parameters);
        _executor = new StatelessExecutor(_weights, parameters);
    }

    public async Task<string> GenerateAsync(
        string query,
        IReadOnlyList<RetrievedChunk> context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(context);

        string prompt = BuildPrompt(query, context);

        await _semaphore.WaitAsync(ct);
        try
        {
            var sb = new StringBuilder();
            await foreach (string token in _executor.InferAsync(prompt, cancellationToken: ct))
            {
                sb.Append(token);
            }
            return sb.ToString();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string BuildPrompt(string query, IReadOnlyList<RetrievedChunk> context)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant.");
        sb.AppendLine("Answer exclusively based on the provided context.");
        sb.AppendLine("If the context does not contain relevant information, say so clearly.");
        sb.AppendLine();
        sb.AppendLine("Context:");

        for (int i = 0; i < context.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {context[i].Chunk.Content}");
        }

        sb.AppendLine();
        sb.Append($"Question: {query}");

        return sb.ToString();
    }

    public void Dispose()
    {
        _weights.Dispose();
        _semaphore.Dispose();
    }
}
