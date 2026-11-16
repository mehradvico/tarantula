using Microsoft.Extensions.Options;
using Tarantula.Infrastructure.ClickHouse;
using Tarantula.Infrastructure.Projects;

namespace Tarantula.Ingest.Api;

/// <summary>
/// The development keys and passwords are committed in appsettings.json so a fresh clone runs.
/// This makes sure they can never reach a real server by accident: outside Development the app refuses to start with them.
/// </summary>
internal static class ProductionGuard
{
    private static readonly string[] DevMarkers = ["_dev_local_only", "tarantula-dev"];

    public static void EnsureNoDevelopmentSecrets(IServiceProvider services)
    {
        var problems = new List<string>();
        foreach (var project in services.GetRequiredService<IOptions<ProjectsOptions>>().Value.Items)
        {
            if (IsDev(project.WriteKey) || IsDev(project.ReadKey))
            {
                problems.Add($"project '{project.Id}' still uses a development key");
            }

            if (project.AllowedOrigins.Count == 0)
            {
                problems.Add($"project '{project.Id}' has no AllowedOrigins (it would accept events from any website)");
            }
        }

        if (IsDev(services.GetRequiredService<IOptions<ClickHouseOptions>>().Value.ConnectionString))
        {
            problems.Add("ClickHouse still uses the development password");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Refusing to start outside Development: " + string.Join("; ", problems) + ".");
        }
    }

    private static bool IsDev(string? value) =>
        !string.IsNullOrEmpty(value) && DevMarkers.Any(m => value.Contains(m, StringComparison.OrdinalIgnoreCase));
}
