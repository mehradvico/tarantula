using System.ComponentModel.DataAnnotations;

namespace Tarantula.Infrastructure.ClickHouse;

public sealed class ClickHouseOptions
{
    public const string SectionName = "Tarantula:ClickHouse";

    /// <summary>Server connection without a database, for example <c>Host=localhost;Port=8123;Username=default</c>.</summary>
    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[A-Za-z_][A-Za-z0-9_]*$", ErrorMessage = "Database must be a plain identifier.")]
    public string Database { get; set; } = "tarantula";

    /// <summary>Events older than this are removed by ClickHouse itself (TTL).</summary>
    [Range(1, 3650)]
    public int RetentionDays { get; set; } = 180;
}
