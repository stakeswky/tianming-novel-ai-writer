using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests;

public class MacOSKeychainApiKeySecretStoreTests
{
    [Fact]
    public void SaveSecret_invokes_security_add_generic_password_with_service_and_account()
    {
        var runner = new CapturingSecurityCommandRunner();
        var store = new MacOSKeychainApiKeySecretStore(runner, serviceName: "tianming.test");

        store.SaveSecret("config-1", "secret");

        var command = Assert.Single(runner.Commands);
        Assert.Equal("/usr/bin/security", command.FileName);
        Assert.Equal(["add-generic-password", "-s", "tianming.test", "-a", "config-1", "-w", "secret", "-U"], command.Arguments);
    }

    [Fact]
    public void GetSecret_returns_stdout_from_security_find_generic_password()
    {
        var runner = new CapturingSecurityCommandRunner
        {
            NextResult = new SecurityCommandResult(0, "secret\n", "")
        };
        var store = new MacOSKeychainApiKeySecretStore(runner, serviceName: "tianming.test");

        var secret = store.GetSecret("config-1");

        Assert.Equal("secret", secret);
        var command = Assert.Single(runner.Commands);
        Assert.Equal(["find-generic-password", "-s", "tianming.test", "-a", "config-1", "-w"], command.Arguments);
    }

    [Fact]
    public void GetSecret_returns_null_when_item_is_missing()
    {
        var runner = new CapturingSecurityCommandRunner
        {
            NextResult = new SecurityCommandResult(44, "", "could not be found")
        };
        var store = new MacOSKeychainApiKeySecretStore(runner, serviceName: "tianming.test");

        var secret = store.GetSecret("config-1");

        Assert.Null(secret);
    }

    [Fact]
    public void DeleteSecret_invokes_security_delete_generic_password()
    {
        var runner = new CapturingSecurityCommandRunner();
        var store = new MacOSKeychainApiKeySecretStore(runner, serviceName: "tianming.test");

        store.DeleteSecret("config-1");

        var command = Assert.Single(runner.Commands);
        Assert.Equal(["delete-generic-password", "-s", "tianming.test", "-a", "config-1"], command.Arguments);
    }

    private sealed class CapturingSecurityCommandRunner : ISecurityCommandRunner
    {
        public List<SecurityCommandInvocation> Commands { get; } = new();
        public SecurityCommandResult NextResult { get; set; } = new(0, "", "");

        public SecurityCommandResult Run(string fileName, IReadOnlyList<string> arguments)
        {
            Commands.Add(new SecurityCommandInvocation(fileName, arguments.ToArray()));
            return NextResult;
        }
    }

    private sealed record SecurityCommandInvocation(string FileName, IReadOnlyList<string> Arguments);
}
