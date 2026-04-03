# 02 — Coding Conventions

## General C# Rules

- **Target framework:** `net10.0`
- **Nullable reference types:** enabled and must be handled in every project
- **Implicit usings:** enabled
- **File-scoped namespace:** required in every file

## Async

- Every I/O and LLM call must use `async`/`await` — no exceptions
- `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` are forbidden
- Every async method accepts a `CancellationToken` parameter and forwards it

```csharp
// CORRECT
public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)

// FORBIDDEN
public float[] Embed(string text) => EmbedAsync(text).Result;
```

## Types

- `record` for immutable domain models (Document, DocumentChunk, etc.)
- `readonly struct` for float vectors where performance-critical
- `sealed class` for all concrete implementations not designed for inheritance

## Dependency Injection

- Constructor injection only
- Service locator pattern is forbidden
- Every external dependency (LlamaSharp, HttpClient) is hidden behind an interface
- `IDisposable` implementations managed with `AddSingleton` (LlamaSharp models are expensive)

## Error Handling

- `ArgumentNullException.ThrowIfNull()` for parameter validation
- Domain-specific exceptions: `RagException` base class in Core
- LlamaSharp and HTTP errors are wrapped in the Infrastructure layer
- Never swallow exceptions (empty catch blocks are forbidden)

## Naming

| Element | Convention | Example |
|---|---|---|
| Interface | `I` prefix | `IEmbedder` |
| Implementation | technology prefix | `LlamaEmbedder` |
| Async method | `Async` suffix | `EmbedAsync` |
| Record | PascalCase | `DocumentChunk` |
| Constant | PascalCase | `DefaultChunkSize` |

## Project File Rules

```xml
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
</PropertyGroup>
```

`TreatWarningsAsErrors` is required — nullability warnings must not pass as warnings.
