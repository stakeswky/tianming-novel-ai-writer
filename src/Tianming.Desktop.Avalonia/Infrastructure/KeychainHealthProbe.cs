using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class KeychainHealthProbe : IKeychainHealthProbe
{
    private const string ProbeKey = "__tianming_health_probe__";
    private readonly IApiKeySecretStore _store;

    public KeychainHealthProbe(IApiKeySecretStore store) { _store = store; }

    public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            // 调一次 GetSecret 来确认 security tool 可用
            // 返回 null（未找到）也算可用，只要不抛异常
            _ = _store.GetSecret(ProbeKey);
            return Task.FromResult(new StatusIndicator("Keychain", StatusKind.Success, "macOS Keychain 可用"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new StatusIndicator("Keychain", StatusKind.Danger, $"Keychain 不可用：{ex.Message}"));
        }
    }
}
