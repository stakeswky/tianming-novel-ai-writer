using System.Text.Json;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableOAuthProviderConfigStoreTests
{
    [Fact]
    public void Constructor_returns_default_providers_when_file_is_missing()
    {
        using var temp = new TempDirectory();

        var store = CreateStore(temp);
        var providers = store.GetProviders();

        Assert.Contains("github", providers.Keys);
        Assert.Contains("wechat", providers.Keys);
        Assert.Equal("https://github.com/login/oauth/authorize", providers["github"].AuthUrl);
        Assert.Equal("user:email", providers["github"].Scope);
        Assert.All(providers.Values, provider => Assert.Equal(string.Empty, provider.ClientId));
        Assert.False(File.Exists(Path.Combine(temp.Path, "oauth_providers.json")));
    }

    [Fact]
    public void ConfigurePlatform_persists_client_id_and_configuration_status()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        Assert.True(store.ConfigurePlatform("GitHub", "client-1"));
        var reloaded = CreateStore(temp);

        Assert.True(reloaded.IsPlatformConfigured("github"));
        Assert.Equal("client-1", reloaded.GetProvider("github")!.ClientId);
    }

    [Fact]
    public void ConfigurePlatform_preserves_default_auth_url_and_scope()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.ConfigurePlatform("github", "client-1");
        var reloaded = CreateStore(temp);
        var provider = reloaded.GetProvider("github")!;

        Assert.Equal("https://github.com/login/oauth/authorize", provider.AuthUrl);
        Assert.Equal("user:email", provider.Scope);
    }

    [Fact]
    public void ConfigurePlatform_rejects_unknown_platform_without_creating_file()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        var configured = store.ConfigurePlatform("unknown", "client-1");

        Assert.False(configured);
        Assert.Null(store.GetProvider("unknown"));
        Assert.False(File.Exists(Path.Combine(temp.Path, "oauth_providers.json")));
    }

    [Fact]
    public void UpdateProviderConfig_persists_custom_known_provider_settings()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        Assert.True(store.UpdateProviderConfig("github", new PortableOAuthProviderConfig
        {
            AuthUrl = "https://oauth.example.test/authorize",
            ClientId = "custom-client",
            Scope = "repo user:email"
        }));
        var reloaded = CreateStore(temp);
        var provider = reloaded.GetProvider("github")!;

        Assert.True(reloaded.IsPlatformConfigured("github"));
        Assert.Equal("https://oauth.example.test/authorize", provider.AuthUrl);
        Assert.Equal("custom-client", provider.ClientId);
        Assert.Equal("repo user:email", provider.Scope);
    }

    [Fact]
    public void Constructor_recovers_defaults_from_invalid_json()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "oauth_providers.json"), "{bad json");

        var store = CreateStore(temp);
        var github = store.GetProvider("github")!;

        Assert.Equal("https://github.com/login/oauth/authorize", github.AuthUrl);
        Assert.Equal("user:email", github.Scope);
        Assert.False(store.IsPlatformConfigured("github"));
    }

    [Fact]
    public void GetProviders_returns_defensive_copies()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        var providers = store.GetProviders();
        providers["github"].ClientId = "mutated";

        Assert.False(store.IsPlatformConfigured("github"));
        Assert.Equal(string.Empty, store.GetProvider("github")!.ClientId);
    }

    [Fact]
    public void Save_writes_pascal_case_provider_fields()
    {
        using var temp = new TempDirectory();
        var store = CreateStore(temp);

        store.ConfigurePlatform("github", "client-1");

        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(temp.Path, "oauth_providers.json")));
        var github = document.RootElement.GetProperty("Providers").GetProperty("github");
        Assert.Equal("https://github.com/login/oauth/authorize", github.GetProperty("AuthUrl").GetString());
        Assert.Equal("client-1", github.GetProperty("ClientId").GetString());
        Assert.Equal("user:email", github.GetProperty("Scope").GetString());
    }

    private static PortableOAuthProviderConfigStore CreateStore(TempDirectory temp)
    {
        return new PortableOAuthProviderConfigStore(Path.Combine(temp.Path, "oauth_providers.json"));
    }
}
