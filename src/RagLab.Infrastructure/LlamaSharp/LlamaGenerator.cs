using System.Text;
using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;
using RagLab.Core.Interfaces;
using RagLab.Core.Models;

namespace RagLab.Infrastructure.LlamaSharp;

public sealed class LlamaGenerator : IGenerator, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly ModelParams _parameters;
    private readonly StatelessExecutor? _executor;
    private readonly bool _applyChatTemplate;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LlamaGenerator(IOptions<LlamaSharpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var opts = options.Value;
        _applyChatTemplate = opts.ApplyChatTemplate;

        _parameters = new ModelParams(opts.GenerationModelPath)
        {
            ContextSize = (uint)opts.ContextSize,
            GpuLayerCount = opts.GpuLayerCount
        };

        _weights = LLamaWeights.LoadFromFile(_parameters);

        if (!_applyChatTemplate)
            _executor = new StatelessExecutor(_weights, _parameters);
    }

    public async Task<string> GenerateAsync(
        string query,
        IReadOnlyList<RetrievedChunk> documentContext,
        IReadOnlyList<RetrievedChunk> historyContext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(documentContext);
        ArgumentNullException.ThrowIfNull(historyContext);

        string prompt = BuildPrompt(query, documentContext, historyContext);

        await _semaphore.WaitAsync(ct);
        try
        {
            var sb = new StringBuilder();

            if (_applyChatTemplate)
            {
                using LLamaContext context = _weights.CreateContext(_parameters);
                var executor = new InteractiveExecutor(context);
                var session = new ChatSession(executor);
                var message = new ChatHistory.Message(AuthorRole.User, prompt);

                await foreach (string token in session.ChatAsync(message, inferenceParams: null, ct))
                    sb.Append(token);
            }
            else
            {
                await foreach (string token in _executor!.InferAsync(prompt, cancellationToken: ct))
                    sb.Append(token);
            }

            return sb.ToString();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string BuildPrompt(
        string query,
        IReadOnlyList<RetrievedChunk> documentContext,
        IReadOnlyList<RetrievedChunk> historyContext)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are a helpful assistant.");
        sb.AppendLine("Answer exclusively based on the provided context.");
        sb.AppendLine("If the context does not contain relevant information, say so clearly.");
        sb.AppendLine();
        sb.AppendLine("Document Context:");

        for (int i = 0; i < documentContext.Count; i++)
        {
            sb.AppendLine($"[{i + 1}] {documentContext[i].Chunk.Content}");
        }

        if (historyContext.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Conversation History:");
            for (int i = 0; i < historyContext.Count; i++)
            {
                sb.AppendLine($"[H{i + 1}] {historyContext[i].Chunk.Content}");
            }
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
