using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RagLab.Console;
using RagLab.Core.Interfaces;
using RagLab.Infrastructure;
using RagLab.Infrastructure.LlamaSharp;
using RagLab.Infrastructure.Loaders;
using RagLab.Infrastructure.VectorStore;

var builder = Host.CreateApplicationBuilder(args);

IModelSlice slice = new LlamaSlice();
slice.Register(builder.Services, builder.Configuration);

builder.Services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("documents");
builder.Services.AddKeyedSingleton<IVectorStore, InMemoryVectorStore>("history");

builder.Services.AddSingleton<IDocumentLoader, TextDocumentLoader>();
builder.Services.AddSingleton<IDocumentLoader, MarkdownDocumentLoader>();

builder.Services.AddSingleton<IndexingPipeline>();
builder.Services.AddSingleton<QueryPipeline>();

var app = builder.Build();

var indexing = app.Services.GetRequiredService<IndexingPipeline>();
var query = app.Services.GetRequiredService<QueryPipeline>();

await indexing.IndexAsync("docs/sample.md");

string answer = await query.QueryAsync("What are the main components of the RAG pipeline?");
Console.WriteLine(answer);

string answer2 = await query.QueryAsync("Can you summarize what you just told me?");
Console.WriteLine(answer2);
