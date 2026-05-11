using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using TM.Framework.Security;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSslPinningValidatorTests
{
    [Fact]
    public void CalculatePin_returns_spki_sha256_base64()
    {
        using var certificate = CreateCertificate("CN=api.example.test");
        var expected = Convert.ToBase64String(
            SHA256.HashData(certificate.PublicKey.ExportSubjectPublicKeyInfo()));

        Assert.Equal(expected, PortableSslPinningValidator.CalculatePin(certificate));
    }

    [Fact]
    public void Validate_accepts_leaf_certificate_matching_configured_pin()
    {
        using var certificate = CreateCertificate("CN=api.example.test");
        var validator = new PortableSslPinningValidator(
            PortableSslPinningOptions.FromPins([PortableSslPinningValidator.CalculatePin(certificate)]));

        var result = validator.Validate(certificate, [], SslPolicyErrors.None);

        Assert.True(result.IsValid);
        Assert.Equal(PortableSslPinningMatchScope.Leaf, result.MatchScope);
    }

    [Fact]
    public void Validate_accepts_chain_certificate_matching_configured_pin()
    {
        using var leaf = CreateCertificate("CN=api.example.test");
        using var intermediate = CreateCertificate("CN=Example Intermediate");
        var validator = new PortableSslPinningValidator(
            PortableSslPinningOptions.FromPins([PortableSslPinningValidator.CalculatePin(intermediate)]));

        var result = validator.Validate(leaf, [intermediate], SslPolicyErrors.None);

        Assert.True(result.IsValid);
        Assert.Equal(PortableSslPinningMatchScope.Chain, result.MatchScope);
    }

    [Fact]
    public void Validate_rejects_tls_policy_errors_before_pin_matching()
    {
        using var certificate = CreateCertificate("CN=api.example.test");
        var validator = new PortableSslPinningValidator(
            PortableSslPinningOptions.FromPins([PortableSslPinningValidator.CalculatePin(certificate)]));

        var result = validator.Validate(certificate, [], SslPolicyErrors.RemoteCertificateNameMismatch);

        Assert.False(result.IsValid);
        Assert.Equal(PortableSslPinningFailureReason.PolicyError, result.FailureReason);
    }

    [Fact]
    public void Validate_rejects_missing_certificate_and_pin_mismatch()
    {
        using var certificate = CreateCertificate("CN=api.example.test");
        var validator = new PortableSslPinningValidator(
            PortableSslPinningOptions.FromPins(["AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA="]));

        var missing = validator.Validate(null, [], SslPolicyErrors.None);
        var mismatch = validator.Validate(certificate, [], SslPolicyErrors.None);

        Assert.False(missing.IsValid);
        Assert.Equal(PortableSslPinningFailureReason.MissingCertificate, missing.FailureReason);
        Assert.False(mismatch.IsValid);
        Assert.Equal(PortableSslPinningFailureReason.PinMismatch, mismatch.FailureReason);
    }

    [Fact]
    public void Options_ignore_blank_and_placeholder_pins()
    {
        var options = PortableSslPinningOptions.FromPins(
        [
            "",
            "  ",
            "YOUR_SSL_PIN_BASE64_HERE",
            "abc"
        ]);

        Assert.Equal(["abc"], options.AllowedPins);
    }

    [Fact]
    public void ServerConfiguration_builds_options_from_current_and_backup_pins()
    {
        var configuration = new PortableSslPinningServerConfiguration
        {
            Host = "https://api.example.com/v1",
            CurrentPins = ["pin-current"],
            BackupPins = ["pin-backup", "pin-current", "YOUR_SSL_PIN_BASE64_HERE"]
        };

        var options = configuration.CreateOptions(new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal("api.example.com", configuration.NormalizedHost);
        Assert.Equal(["pin-current", "pin-backup"], options.AllowedPins);
    }

    [Fact]
    public void ServerConfiguration_applies_rotation_window_for_next_pins()
    {
        var now = new DateTime(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);
        var configuration = new PortableSslPinningServerConfiguration
        {
            Host = "api.example.com",
            CurrentPins = ["pin-current"],
            RotatingPins =
            [
                new PortableSslPinningRotatingPin("pin-active-next", now.AddHours(-1), now.AddHours(1)),
                new PortableSslPinningRotatingPin("pin-future", now.AddHours(1), null),
                new PortableSslPinningRotatingPin("pin-expired", null, now.AddSeconds(-1))
            ]
        };

        var options = configuration.CreateOptions(now);

        Assert.Equal(["pin-current", "pin-active-next"], options.AllowedPins);
    }

    private static X509Certificate2 CreateCertificate(string subjectName)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            subjectName,
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1));

        return new X509Certificate2(certificate.Export(X509ContentType.Cert));
    }
}
