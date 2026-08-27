using Microsoft.AspNetCore.DataProtection;
using Rema.App.Services.Ai;

namespace Rema.App.Tests;

public class ApiKeyProtectorTests
{
    private static ApiKeyProtector New() => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Protect_then_unprotect_round_trips()
    {
        var p = New();
        var key = "sk-ant-api03-EXAMPLE-1234";

        var stored = p.Protect(key);

        Assert.NotEqual(key, stored);
        Assert.Equal(key, p.TryUnprotect(stored));
    }

    [Fact]
    public void TryUnprotect_returns_null_for_garbage_or_empty()
    {
        var p = New();
        Assert.Null(p.TryUnprotect(null));
        Assert.Null(p.TryUnprotect(""));
        Assert.Null(p.TryUnprotect("not-a-valid-protected-blob"));
    }

    [Fact]
    public void Hint_shows_only_last_four()
    {
        Assert.Equal("····6789", ApiKeyProtector.Hint("sk-ant-123456789"));
        Assert.Equal("····", ApiKeyProtector.Hint("abc"));
    }
}
