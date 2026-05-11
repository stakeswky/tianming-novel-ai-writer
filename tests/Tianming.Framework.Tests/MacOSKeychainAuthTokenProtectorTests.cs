using System.Text;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSKeychainAuthTokenProtectorTests
{
    [Fact]
    public void Protect_saves_base64_payload_to_keychain_and_returns_reference_marker()
    {
        var runner = new CapturingKeychainCommandRunner();
        var protector = new MacOSKeychainAuthTokenProtector(
            runner,
            serviceName: "tianming.test",
            accountName: "auth-token");

        var marker = protector.Protect(Encoding.UTF8.GetBytes("""{"AccessToken":"secret"}"""));

        Assert.Equal("keychain:auth-token", marker);
        var command = Assert.Single(runner.Commands);
        Assert.Equal("/usr/bin/security", command.FileName);
        Assert.Equal(
            [
                "add-generic-password",
                "-s", "tianming.test",
                "-a", "auth-token",
                "-w", Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"AccessToken":"secret"}""")),
                "-U"
            ],
            command.Arguments);
    }

    [Fact]
    public void Unprotect_reads_marker_payload_from_keychain()
    {
        var runner = new CapturingKeychainCommandRunner
        {
            NextResult = new PortableKeychainCommandResult(
                0,
                Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"AccessToken":"secret"}""")) + "\n",
                "")
        };
        var protector = new MacOSKeychainAuthTokenProtector(
            runner,
            serviceName: "tianming.test",
            accountName: "auth-token");

        var data = protector.Unprotect("keychain:auth-token");

        Assert.Equal("""{"AccessToken":"secret"}""", Encoding.UTF8.GetString(data));
        var command = Assert.Single(runner.Commands);
        Assert.Equal(
            [
                "find-generic-password",
                "-s", "tianming.test",
                "-a", "auth-token",
                "-w"
            ],
            command.Arguments);
    }

    [Fact]
    public void Unprotect_uses_marker_account_over_default_account()
    {
        var runner = new CapturingKeychainCommandRunner
        {
            NextResult = new PortableKeychainCommandResult(0, Convert.ToBase64String([1, 2, 3]), "")
        };
        var protector = new MacOSKeychainAuthTokenProtector(
            runner,
            serviceName: "tianming.test",
            accountName: "default-account");

        var data = protector.Unprotect("keychain:legacy-account");

        Assert.Equal([1, 2, 3], data);
        Assert.Equal("legacy-account", runner.Commands[0].Arguments[4]);
    }

    [Fact]
    public void Delete_removes_token_payload_from_keychain()
    {
        var runner = new CapturingKeychainCommandRunner();
        var protector = new MacOSKeychainAuthTokenProtector(
            runner,
            serviceName: "tianming.test",
            accountName: "auth-token");

        protector.Delete();

        var command = Assert.Single(runner.Commands);
        Assert.Equal(
            [
                "delete-generic-password",
                "-s", "tianming.test",
                "-a", "auth-token"
            ],
            command.Arguments);
    }

    [Fact]
    public void Token_store_with_keychain_protector_writes_reference_file_not_token_json()
    {
        using var temp = new TempDirectory();
        var runner = new CapturingKeychainCommandRunner();
        var protector = new MacOSKeychainAuthTokenProtector(
            runner,
            serviceName: "tianming.test",
            accountName: "auth-token");
        var store = new PortableAuthTokenStore(
            Path.Combine(temp.Path, "auth_token.dat"),
            protector,
            () => new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc),
            () => "client-1",
            () => "nonce-1");

        store.SaveTokens(new PortableLoginResult
        {
            AccessToken = "access-secret",
            RefreshToken = "refresh-secret",
            SessionKey = "session-secret",
            ExpiresAt = new DateTime(2026, 5, 10, 13, 0, 0, DateTimeKind.Utc),
            User = new PortableUserInfo { UserId = "u1", Username = "jimmy" }
        });

        var fileContent = File.ReadAllText(Path.Combine(temp.Path, "auth_token.dat"));

        Assert.Equal("keychain:auth-token", fileContent);
        Assert.DoesNotContain("access-secret", fileContent);
        Assert.Equal(2, runner.Commands.Count);
        Assert.All(runner.Commands, command => Assert.Equal("add-generic-password", command.Arguments[0]));
    }

    private sealed class CapturingKeychainCommandRunner : IPortableKeychainCommandRunner
    {
        public List<PortableKeychainCommandInvocation> Commands { get; } = new();
        public PortableKeychainCommandResult NextResult { get; set; } = new(0, "", "");

        public PortableKeychainCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Commands.Add(new PortableKeychainCommandInvocation(fileName, arguments.ToArray()));
            return NextResult;
        }
    }
}
