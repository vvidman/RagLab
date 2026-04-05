using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RagLab.Core.Interfaces;
using RagLab.Infrastructure.Chunking;

namespace RagLab.Infrastructure.LlamaSharp;

public sealed class LlamaSlice : IModelSlice
{
    public int RecommendedTopK => 3;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlamaSharpOptions>(configuration.GetSection("LlamaSharp"));

        services.AddSingleton<LlamaEmbedder>();
        services.AddSingleton<IEmbedder>(sp => sp.GetRequiredService<LlamaEmbedder>());

        services.AddSingleton<LlamaGenerator>();
        services.AddSingleton<IGenerator>(sp => sp.GetRequiredService<LlamaGenerator>());

        services.AddSingleton<IChunker>(_ => new FixedSizeChunker(new FixedSizeChunkerOptions
        {
            ChunkSize = 512,
            Overlap = 64
        }));
    }
}
