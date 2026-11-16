namespace Tarantula.Application.Abstractions;

/// <summary>A tracked product (a website or an app) that sends events with a public write key.</summary>
public sealed record ProjectInfo(string Id, string Name, IReadOnlyCollection<string> AllowedOrigins)
{
    /// <summary>
    /// A missing <paramref name="origin"/> (server to server) is allowed. An empty allow list means
    /// "any origin", which is only meant for local development.
    /// </summary>
    public bool AllowsOrigin(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || AllowedOrigins.Count == 0)
        {
            return true;
        }

        var wanted = origin.TrimEnd('/');
        return AllowedOrigins.Any(o => string.Equals(o.TrimEnd('/'), wanted, StringComparison.OrdinalIgnoreCase));
    }
}

public interface IProjectRegistry
{
    /// <returns>The project that owns <paramref name="writeKey"/>, or <c>null</c> when the key is unknown.</returns>
    Task<ProjectInfo?> FindByWriteKeyAsync(string writeKey, CancellationToken cancellationToken);

    /// <returns>The project that owns the secret <paramref name="readKey"/>, or <c>null</c> when it is unknown.</returns>
    Task<ProjectInfo?> FindByReadKeyAsync(string readKey, CancellationToken cancellationToken);
}
