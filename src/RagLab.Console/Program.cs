using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagLab.Console;
using RagLab.Core.Interfaces;
using RagLab.Infrastructure.Chunking;
using RagLab.Infrastructure.LlamaSharp;
using RagLab.Infrastructure.Loaders;
using RagLab.Infrastructure.VectorStore;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<LlamaSharpOptions>(builder.Configuration.GetSection("LlamaSharp"));
builder.Services.Configure<FixedSizeChunkerOptions>(builder.Configuration.GetSection("Chunker"));

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlamaSharpOptions>>().Value;
    return new LlamaEmbedder(opts);
});
builder.Services.AddSingleton<IEmbedder>(sp => sp.GetRequiredService<LlamaEmbedder>());

builder.Services.AddSingleton(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<LlamaSharpOptions>>().Value;
    return new LlamaGenerator(opts);
});
builder.Services.AddSingleton<IGenerator>(sp => sp.GetRequiredService<LlamaGenerator>());

builder.Services.AddSingleton<InMemoryVectorStore>();
builder.Services.AddSingleton<IVectorStore>(sp => sp.GetRequiredService<InMemoryVectorStore>());

builder.Services.AddSingleton<IDocumentLoader, TextDocumentLoader>();
builder.Services.AddSingleton<IDocumentLoader, MarkdownDocumentLoader>();

builder.Services.AddSingleton<IChunker>(sp =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FixedSizeChunkerOptions>>().Value;
    return new FixedSizeChunker(opts);
});

builder.Services.AddSingleton<RagPipeline>();

var app = builder.Build();

var pipeline = app.Services.GetRequiredService<RagPipeline>();

await pipeline.IndexAsync("docs/sample.md");

string answer = await pipeline.QueryAsync("What are the main components of the RAG pipeline?");
Console.WriteLine();
Console.WriteLine("Answer:");
Console.WriteLine(answer);
