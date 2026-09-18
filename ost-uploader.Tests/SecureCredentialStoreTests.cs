using System.Net;
using System.Security;
using ost_uploader;

namespace ost_uploader.Tests;

public class SecureCredentialStoreTests
{
    [Fact]
    public void SaveToken_WithCredentials_RestoresEmailAndPassword()
    {
        var store = new SecureCredentialStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var password = new NetworkCredential(string.Empty, "secret-password").SecurePassword;
        var auth = new APIAuthResponse
        {
            token = "token",
            expiration = DateTime.UtcNow.AddHours(1).ToString("O")
        };

        Assert.True(store.SaveToken(auth, "https://example.test", "user@example.test", password));
        Assert.True(store.TryGetSavedCredentials("https://example.test", out var email, out var savedPassword, out var savedAuth));
        Assert.Equal("user@example.test", email);
        Assert.Equal("secret-password", new NetworkCredential(string.Empty, savedPassword).Password);
        Assert.Equal("token", savedAuth.token);
    }

    [Fact]
    public void SaveToken_WithCredentials_RestoresAcrossEventGroupsInSameEnvironment()
    {
        var store = new SecureCredentialStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var password = new NetworkCredential(string.Empty, "secret-password").SecurePassword;
        var auth = new APIAuthResponse
        {
            token = "token",
            expiration = DateTime.UtcNow.AddHours(1).ToString("O")
        };

        Assert.True(store.SaveToken(auth, "https://staging.opensplittime.org/api/v1/event_groups/10", "user@example.test", password));
        Assert.True(store.TryGetSavedCredentials("https://staging.opensplittime.org/api/v1/event_groups/20", out var email, out var savedPassword, out var savedAuth));
        Assert.Equal("user@example.test", email);
        Assert.Equal("secret-password", new NetworkCredential(string.Empty, savedPassword).Password);
        Assert.Equal("token", savedAuth.token);
    }

    [Fact]
    public void TokenOnlyCredentials_AreNotReportedAsSavedLoginCredentials()
    {
        var store = new SecureCredentialStore(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
        var auth = new APIAuthResponse
        {
            token = "token",
            expiration = DateTime.UtcNow.AddHours(1).ToString("O")
        };

        Assert.True(store.SaveToken(auth, "https://example.test"));
        Assert.False(store.TryGetSavedCredentials("https://example.test", out _, out _, out _));
    }
}