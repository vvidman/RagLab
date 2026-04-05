using LLama;
using LLama.Common;
using Microsoft.Extensions.Options;
using RagLab.Core.Interfaces;

namespace RagLab.Infrastructure.LlamaSharp;

public sealed class LlamaEmbedder : IEmbedder, IDisposable
{
    private readonly LLamaWeights _weights;
    private readonly LLamaEmbedder _embedder;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public LlamaEmbedder(IOptions<LlamaSharpOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var parameters = new ModelParams(options.Value.EmbeddingModelPath)
        {
            ContextSize = (uint)options.Value.ContextSize,
            GpuLayerCount = options.Value.GpuLayerCount
        };

        _weights = LLamaWeights.LoadFromFile(parameters);
        _embedder = new LLamaEmbedder(_weights, parameters);
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(text);

        await _semaphore.WaitAsync(ct);
        try
        {
            IReadOnlyList<float[]> tokenEmbeddings = await _embedder.GetEmbeddings(text, ct);
            return MeanPool(tokenEmbeddings);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(texts);

        var results = new float[texts.Count][];
        for (int i = 0; i < texts.Count; i++)
        {
            results[i] = await EmbedAsync(texts[i], ct);
        }
        return results;
    }

    private static float[] MeanPool(IReadOnlyList<float[]> tokenEmbeddings)
    {
        if (tokenEmbeddings.Count == 0)
            return [];

        int dim = tokenEmbeddings[0].Length;
        float[] mean = new float[dim];

        foreach (float[] token in tokenEmbeddings)
            for (int i = 0; i < dim; i++)
                mean[i] += token[i];

        for (int i = 0; i < dim; i++)
            mean[i] /= tokenEmbeddings.Count;

        return mean;
    }

    public void Dispose()
    {
        _embedder.Dispose();
        _weights.Dispose();
        _semaphore.Dispose();
    }
}
