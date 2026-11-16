using Tarantula.Domain.Events;

namespace Tarantula.Domain.Tests;

public class EventNameTests
{
    [Theory]
    [InlineData("$pageview")]
    [InlineData("$click")]
    [InlineData("add_to_cart")]
    [InlineData("checkout.started")]
    [InlineData("Vendor:profile-viewed")]
    public void Accepts_valid_names(string raw)
    {
        Assert.True(EventName.TryCreate(raw, out var name));
        Assert.Equal(raw, name.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("$")]
    [InlineData("1click")]
    [InlineData("has space")]
    [InlineData("emoji😀")]
    [InlineData("بازدید")]
    public void Rejects_invalid_names(string? raw)
    {
        Assert.False(EventName.TryCreate(raw, out _));
    }

    [Fact]
    public void Rejects_names_longer_than_the_limit()
    {
        Assert.True(EventName.TryCreate(new string('a', EventName.MaxLength), out _));
        Assert.False(EventName.TryCreate(new string('a', EventName.MaxLength + 1), out _));
    }

    [Fact]
    public void System_events_start_with_dollar()
    {
        EventName.TryCreate("$pageview", out var system);
        EventName.TryCreate("pageview", out var custom);

        Assert.True(system.IsSystem);
        Assert.False(custom.IsSystem);
    }
}
