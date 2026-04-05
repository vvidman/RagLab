using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace RagLab.Infrastructure;

public interface IModelSlice
{
    int RecommendedTopK { get; }
    void Register(IServiceCollection services, IConfiguration configuration);
}
