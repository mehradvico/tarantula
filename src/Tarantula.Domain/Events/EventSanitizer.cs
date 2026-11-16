namespace Tarantula.Domain.Events;

/// <summary>
/// Applies the user's consent to an event before it is stored. Pure and deterministic,
/// so the privacy rule can be unit tested without any infrastructure.
/// </summary>
public static class EventSanitizer
{
    /// <returns>
    /// The event to store, or <c>null</c> when the user refused tracking
    /// (<see cref="ConsentLevel.None"/>): the event must be dropped.
    /// </returns>
    public static TrackedEvent? ApplyConsent(TrackedEvent source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source.Consent switch
        {
            ConsentLevel.None => null,

            ConsentLevel.Anonymous => source with
            {
                UserId = null,
                VendorId = null,
                UserAgent = null,
                Url = StripQuery(source.Url),
                Referrer = HostOnly(source.Referrer),
                PropertiesJson = TrackedEvent.EmptyProperties
            },

            ConsentLevel.Analytics => source with
            {
                UserId = null,
                VendorId = null
            },

            ConsentLevel.Full => source,

            _ => null
        };
    }

    private static string? StripQuery(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        var cut = url.IndexOfAny(['?', '#']);
        return cut < 0 ? url : url[..cut];
    }

    private static string? HostOnly(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        return Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;
    }
}
