using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Tarantula.Application.Abstractions;

namespace Tarantula.Infrastructure.Projects;

public sealed class ProjectOptions
{
    [Required]
    public string Id { get; set; } = string.Empty;

    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Public key placed in the SDK snippet. It only allows writing events.</summary>
    [Required]
    [MinLength(8)]
    public string WriteKey { get; set; } = string.Empty;

    /// <summary>
    /// Secret key that allows reading (live users, later reports). Never put it in a page served to visitors.
    /// Empty = reading is disabled for this project.
    /// </summary>
    public string ReadKey { get; set; } = string.Empty;

    /// <summary>Browser origins allowed to send events. Empty = any origin (development only).</summary>
    public List<string> AllowedOrigins { get; set; } = [];
}

public sealed class ProjectsOptions
{
    public const string SectionName = "Tarantula:Projects";

    public List<ProjectOptions> Items { get; set; } = [];
}

/// <summary>Reads projects from configuration. A database-backed registry replaces it when the console exists.</summary>
internal sealed class ConfigProjectRegistry : IProjectRegistry
{
    private readonly Dictionary<string, ProjectInfo> _byWriteKey;
    private readonly List<(byte[] KeyBytes, ProjectInfo Project)> _byReadKey;

    public ConfigProjectRegistry(IOptions<ProjectsOptions> options)
    {
        var items = options.Value.Items;
        _byWriteKey = items.ToDictionary(p => p.WriteKey, ToInfo, StringComparer.Ordinal);
        _byReadKey = items
            .Where(p => !string.IsNullOrEmpty(p.ReadKey))
            .Select(p => (Encoding.UTF8.GetBytes(p.ReadKey), ToInfo(p)))
            .ToList();
    }

    public Task<ProjectInfo?> FindByWriteKeyAsync(string writeKey, CancellationToken cancellationToken) =>
        Task.FromResult(_byWriteKey.GetValueOrDefault(writeKey));

    public Task<ProjectInfo?> FindByReadKeyAsync(string readKey, CancellationToken cancellationToken)
    {
        // The read key is a secret: compare in constant time so response timing cannot leak it.
        var candidate = Encoding.UTF8.GetBytes(readKey ?? string.Empty);
        ProjectInfo? match = null;
        foreach (var (keyBytes, project) in _byReadKey)
        {
            if (candidate.Length == keyBytes.Length && CryptographicOperations.FixedTimeEquals(candidate, keyBytes))
            {
                match = project;
            }
        }

        return Task.FromResult(match);
    }

    private static ProjectInfo ToInfo(ProjectOptions p) => new(p.Id, p.Name, p.AllowedOrigins.AsReadOnly());
}
