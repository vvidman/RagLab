using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// TODO: register RagLab services here

var app = builder.Build();

// TODO: run RagPipeline here

await app.RunAsync();
