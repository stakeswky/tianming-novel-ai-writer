using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TM.Framework.Security;

public sealed class PortableSslPinningOptions
{
    private const string PlaceholderPin = "YOUR_SSL_PIN_BASE64_HERE";

    public IReadOnlyList<string> AllowedPins { get; init; } = [];

    public static PortableSslPinningOptions FromPins(IEnumerable<string> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);
        return new PortableSslPinningOptions
        {
            AllowedPins = pins
                .Select(pin => pin.Trim())
                .Where(pin => !string.IsNullOrWhiteSpace(pin))
                .Where(pin => !string.Equals(pin, PlaceholderPin, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToList()
        };
    }
}

public sealed record PortableSslPinningRotatingPin(
    string Pin,
    DateTime? NotBeforeUtc = null,
    DateTime? NotAfterUtc = null)
{
    public bool IsActive(DateTime nowUtc)
    {
        return (!NotBeforeUtc.HasValue || nowUtc >= NotBeforeUtc.Value)
            && (!NotAfterUtc.HasValue || nowUtc <= NotAfterUtc.Value);
    }
}

public sealed class PortableSslPinningServerConfiguration
{
    public string Host { get; init; } = string.Empty;
    public IReadOnlyList<string> CurrentPins { get; init; } = [];
    public IReadOnlyList<string> BackupPins { get; init; } = [];
    public IReadOnlyList<PortableSslPinningRotatingPin> RotatingPins { get; init; } = [];

    public string NormalizedHost => NormalizeHost(Host);

    public PortableSslPinningOptions CreateOptions(DateTime nowUtc)
    {
        var activeRotatingPins = RotatingPins
            .Where(pin => pin.IsActive(nowUtc))
            .Select(pin => pin.Pin);

        return PortableSslPinningOptions.FromPins(
            CurrentPins
                .Concat(BackupPins)
                .Concat(activeRotatingPins));
    }

    private static string NormalizeHost(string host)
    {
        var trimmed = (host ?? string.Empty).Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Host))
        {
            return uri.Host.ToLowerInvariant();
        }

        var withoutPath = trimmed.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        return withoutPath.Trim().ToLowerInvariant();
    }
}

public enum PortableSslPinningMatchScope
{
    None,
    Leaf,
    Chain
}

public enum PortableSslPinningFailureReason
{
    None,
    PolicyError,
    MissingCertificate,
    MissingPins,
    PinMismatch
}

public sealed class PortableSslPinningResult
{
    public bool IsValid { get; init; }
    public PortableSslPinningMatchScope MatchScope { get; init; }
    public PortableSslPinningFailureReason FailureReason { get; init; }
    public string? MatchedPin { get; init; }
}

public sealed class PortableSslPinningValidator
{
    private readonly HashSet<string> _allowedPins;

    public PortableSslPinningValidator(PortableSslPinningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _allowedPins = new HashSet<string>(options.AllowedPins, StringComparer.Ordinal);
    }

    public PortableSslPinningResult Validate(
        X509Certificate2? certificate,
        IEnumerable<X509Certificate2>? chainCertificates,
        SslPolicyErrors policyErrors)
    {
        if (policyErrors != SslPolicyErrors.None)
        {
            return Fail(PortableSslPinningFailureReason.PolicyError);
        }

        if (certificate == null)
        {
            return Fail(PortableSslPinningFailureReason.MissingCertificate);
        }

        if (_allowedPins.Count == 0)
        {
            return Fail(PortableSslPinningFailureReason.MissingPins);
        }

        var leafPin = CalculatePin(certificate);
        if (_allowedPins.Contains(leafPin))
        {
            return Pass(PortableSslPinningMatchScope.Leaf, leafPin);
        }

        foreach (var chainCertificate in chainCertificates ?? [])
        {
            var chainPin = CalculatePin(chainCertificate);
            if (_allowedPins.Contains(chainPin))
            {
                return Pass(PortableSslPinningMatchScope.Chain, chainPin);
            }
        }

        return Fail(PortableSslPinningFailureReason.PinMismatch);
    }

    public static string CalculatePin(X509Certificate2 certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        var spki = certificate.PublicKey.ExportSubjectPublicKeyInfo();
        var hash = SHA256.HashData(spki);
        return Convert.ToBase64String(hash);
    }

    public static HttpClientHandler CreatePinnedHandler(
        PortableSslPinningOptions options,
        bool useProxy = true)
    {
        var validator = new PortableSslPinningValidator(options);
        return new HttpClientHandler
        {
            UseProxy = useProxy,
            ServerCertificateCustomValidationCallback = (_, certificate, chain, policyErrors) =>
            {
                var chainCertificates = chain?.ChainElements
                    .Cast<X509ChainElement>()
                    .Select(element => element.Certificate);

                return validator.Validate(certificate, chainCertificates, policyErrors).IsValid;
            }
        };
    }

    private static PortableSslPinningResult Pass(PortableSslPinningMatchScope scope, string matchedPin)
    {
        return new PortableSslPinningResult
        {
            IsValid = true,
            MatchScope = scope,
            FailureReason = PortableSslPinningFailureReason.None,
            MatchedPin = matchedPin
        };
    }

    private static PortableSslPinningResult Fail(PortableSslPinningFailureReason reason)
    {
        return new PortableSslPinningResult
        {
            IsValid = false,
            MatchScope = PortableSslPinningMatchScope.None,
            FailureReason = reason
        };
    }
}
