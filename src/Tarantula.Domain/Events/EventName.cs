using System.Text.RegularExpressions;

namespace Tarantula.Domain.Events;

/// <summary>
/// Validated event name. System events start with <c>$</c> (for example <c>$pageview</c>);
/// custom events start with a letter. Only letters, digits and <c>_ . : -</c> are allowed.
/// </summary>
public readonly partial record struct EventName
{
    public const int MaxLength = 64;

    public string Value { get; }

    private EventName(string value) => Value = value;

    public bool IsSystem => Value.StartsWith('$');

    public static bool TryCreate(string? raw, out EventName name)
    {
        name = default;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > MaxLength || !Pattern().IsMatch(raw))
        {
            return false;
        }

        name = new EventName(raw);
        return true;
    }

    public override string ToString() => Value ?? string.Empty;

    [GeneratedRegex(@"^\$?[A-Za-z][A-Za-z0-9_.:\-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
