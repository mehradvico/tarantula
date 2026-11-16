using Tarantula.Domain.Events;

namespace Tarantula.Domain.Tests;

public class EventSanitizerTests
{
    private static TrackedEvent Sample(ConsentLevel consent)
    {
        EventName.TryCreate("$pageview", out var name);
        return new TrackedEvent
        {
            Id = Guid.NewGuid(),
            ProjectId = "p1",
            Name = name,
            OccurredAt = DateTimeOffset.UnixEpoch,
            ReceivedAt = DateTimeOffset.UnixEpoch,
            AnonymousId = "anon-1",
            SessionId = "sess-1",
            UserId = "user-42",
            VendorId = "vendor-7",
            Url = "https://shop.example/cart?coupon=SECRET#top",
            Referrer = "https://google.com/search?q=cat+food",
            UserAgent = "TestAgent/1.0",
            Consent = consent,
            PropertiesJson = """{"plan":"gold"}"""
        };
    }

    [Fact]
    public void None_drops_the_event()
    {
        Assert.Null(EventSanitizer.ApplyConsent(Sample(ConsentLevel.None)));
    }

    [Fact]
    public void Anonymous_removes_identifiers_query_strings_and_properties()
    {
        var clean = EventSanitizer.ApplyConsent(Sample(ConsentLevel.Anonymous));

        Assert.NotNull(clean);
        Assert.Null(clean.UserId);
        Assert.Null(clean.VendorId);
        Assert.Null(clean.UserAgent);
        Assert.Equal("https://shop.example/cart", clean.Url);
        Assert.Equal("google.com", clean.Referrer);
        Assert.Equal(TrackedEvent.EmptyProperties, clean.PropertiesJson);
    }

    [Fact]
    public void Analytics_keeps_behaviour_but_not_who_the_user_is()
    {
        var clean = EventSanitizer.ApplyConsent(Sample(ConsentLevel.Analytics));

        Assert.NotNull(clean);
        Assert.Null(clean.UserId);
        Assert.Null(clean.VendorId);
        Assert.Equal("anon-1", clean.AnonymousId);
        Assert.Equal("""{"plan":"gold"}""", clean.PropertiesJson);
    }

    [Fact]
    public void Full_keeps_everything()
    {
        var source = Sample(ConsentLevel.Full);

        Assert.Same(source, EventSanitizer.ApplyConsent(source));
    }

    [Fact]
    public void Unknown_consent_value_is_treated_as_refusal()
    {
        Assert.Null(EventSanitizer.ApplyConsent(Sample((ConsentLevel)99)));
    }
}
