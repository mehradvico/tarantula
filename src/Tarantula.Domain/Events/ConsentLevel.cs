namespace Tarantula.Domain.Events;

/// <summary>
/// How much the end user agreed to be tracked. The level travels with every event and decides
/// what may be stored (see <see cref="EventSanitizer"/>). Privacy is a domain rule, not a UI option.
/// </summary>
public enum ConsentLevel
{
    /// <summary>User refused tracking. Nothing is stored.</summary>
    None = 0,

    /// <summary>Only anonymous, aggregate-grade data: no identifiers, no query strings, no custom properties.</summary>
    Anonymous = 1,

    /// <summary>Behaviour analytics with the anonymous id and session, but no link to a known User or Vendor.</summary>
    Analytics = 2,

    /// <summary>Full tracking including the link to a known User or Vendor.</summary>
    Full = 3
}
