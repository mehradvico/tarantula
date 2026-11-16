using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tarantula.Application.Abstractions;
using Tarantula.Infrastructure.ClickHouse;
using Tarantula.Infrastructure.Pastil;
using Tarantula.Infrastructure.Projects;

namespace Tarantula.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ClickHouseOptions>()
            .Bind(configuration.GetSection(ClickHouseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ProjectsOptions>()
            .Bind(configuration.GetSection(ProjectsOptions.SectionName))
            .ValidateOnStart();

        // ClickHouse compresses its answers, so the handler must decompress them. Pooled connections are
        // recycled every few minutes so DNS changes are picked up.
        services.AddHttpClient(ClickHouseEventStore.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            });
        services.AddSingleton<IEventStore, ClickHouseEventStore>();
        services.AddSingleton<ClickHouseLiveStatsReader>();
        services.AddSingleton<ILiveStatsReader>(sp => new CachedLiveStatsReader(
            sp.GetRequiredService<ClickHouseLiveStatsReader>(), sp.GetRequiredService<TimeProvider>()));
        services.AddHostedService<ClickHouseSchemaInitializer>();
        services.AddSingleton<IProjectRegistry, ConfigProjectRegistry>();

        // The product's own business data (Pastil), through read-only views. Optional: without a connection string
        // the use cases answer "business_not_connected".
        services.Configure<PastilOptions>(configuration.GetSection(PastilOptions.SectionName));
        services.AddSingleton<PastilBusinessReader>();
        services.AddSingleton<IBusinessReader>(sp => new CachedBusinessReader(
            sp.GetRequiredService<PastilBusinessReader>(), sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
