using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortablePasswordData
{
    [JsonPropertyName("PasswordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [JsonPropertyName("Salt")]
    public string Salt { get; set; } = string.Empty;

    [JsonPropertyName("LastModifiedTime")]
    public DateTime LastModifiedTime { get; set; }

    [JsonPropertyName("Iterations")]
    public int Iterations { get; set; } = 100000;

    [JsonPropertyName("HashAlgorithm")]
    public string HashAlgorithm { get; set; } = "PBKDF2";
}

public sealed class PortableTwoFactorAuthData
{
    [JsonPropertyName("Secret")]
    public string Secret { get; set; } = string.Empty;

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("EnabledTime")]
    public DateTime EnabledTime { get; set; }
}

public enum PortablePasswordChangePresentation
{
    Success,
    Warning,
    Error
}

public sealed class PortablePasswordChangeResult
{
    public bool Success { get; init; }
    public string Title { get; init; } = "修改密码";
    public string Message { get; init; } = string.Empty;
    public bool ClearPasswordFields { get; init; }
    public PortablePasswordChangePresentation Presentation { get; init; } = PortablePasswordChangePresentation.Error;
}

public enum PortableTwoFactorPresentation
{
    Success,
    Info,
    Warning,
    Error
}

public sealed class PortableTwoFactorResult
{
    public bool Success { get; init; }
    public string Title { get; init; } = "双因素认证";
    public string Message { get; init; } = string.Empty;
    public string Secret { get; init; } = string.Empty;
    public string FormattedSecret { get; init; } = string.Empty;
    public string? TotpUri { get; init; }
    public bool ClearVerificationCode { get; init; }
    public PortableTwoFactorPresentation Presentation { get; init; } = PortableTwoFactorPresentation.Error;
}

public interface IPortablePasswordChangeApi
{
    Task<PortableApiResponse<object>> ChangePasswordAsync(
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public static class PortablePasswordStrengthPolicy
{
    public static (bool IsValid, string Message) ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password))
            return (false, "密码不能为空");

        if (password.Length < 8)
            return (false, "密码长度至少为8个字符");

        var hasLower = password.Any(char.IsLower);
        var hasUpper = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSymbol = password.Any(c => !char.IsLetterOrDigit(c));
        if ((hasLower ? 1 : 0) + (hasUpper ? 1 : 0) + (hasDigit ? 1 : 0) + (hasSymbol ? 1 : 0) < 3)
            return (false, "密码强度不足，请使用更复杂的密码");

        return (true, "密码符合要求");
    }
}

public sealed class FilePasswordSecurityStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _passwordHashFile;
    private readonly string _passwordHistoryFile;
    private readonly string _twoFactorSecretFile;

    public FilePasswordSecurityStore(string root)
    {
        var directory = Path.Combine(root, "Framework", "User", "Account", "PasswordSecurity");
        _passwordHashFile = Path.Combine(directory, "password_hash.json");
        _passwordHistoryFile = Path.Combine(directory, "password_history.json");
        _twoFactorSecretFile = Path.Combine(directory, "2fa_secret.json");
    }

    public async Task<PortablePasswordData?> LoadPasswordDataAsync(CancellationToken cancellationToken = default)
    {
        return await LoadNullableAsync<PortablePasswordData>(_passwordHashFile, cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePasswordDataAsync(PortablePasswordData data, CancellationToken cancellationToken = default)
    {
        await SaveAsync(_passwordHashFile, data, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<string>> LoadPasswordHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await LoadNullableAsync<List<string>>(_passwordHistoryFile, cancellationToken).ConfigureAwait(false) ?? [];
    }

    public async Task SavePasswordHistoryAsync(IReadOnlyList<string> history, CancellationToken cancellationToken = default)
    {
        await SaveAsync(_passwordHistoryFile, history.ToList(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<PortableTwoFactorAuthData?> LoadTwoFactorDataAsync(CancellationToken cancellationToken = default)
    {
        return await LoadNullableAsync<PortableTwoFactorAuthData>(_twoFactorSecretFile, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveTwoFactorDataAsync(PortableTwoFactorAuthData data, CancellationToken cancellationToken = default)
    {
        await SaveAsync(_twoFactorSecretFile, data, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<T?> LoadNullableAsync<T>(string path, CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(path))
                return default;

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return default;
        }
        catch (IOException)
        {
            return default;
        }
        catch (UnauthorizedAccessException)
        {
            return default;
        }
    }

    private static async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}

public sealed class PortableAccountSecurityService
{
    private const int DefaultIterations = 100000;
    private readonly FilePasswordSecurityStore _store;
    private readonly Func<DateTime> _clock;

    public PortableAccountSecurityService(FilePasswordSecurityStore store, Func<DateTime>? clock = null)
    {
        _store = store;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task SetInitialPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        var data = CreatePasswordData(password);
        await _store.SavePasswordDataAsync(data, cancellationToken).ConfigureAwait(false);
        await AddToPasswordHistoryAsync(data.PasswordHash, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> VerifyPasswordAsync(string password, CancellationToken cancellationToken = default)
    {
        var data = await _store.LoadPasswordDataAsync(cancellationToken).ConfigureAwait(false);
        if (data == null)
            return false;

        var isLegacySha256 = string.IsNullOrEmpty(data.HashAlgorithm) ||
                             string.Equals(data.HashAlgorithm, "SHA256", StringComparison.OrdinalIgnoreCase);
        var expected = isLegacySha256
            ? HashPasswordSha256(password, data.Salt)
            : HashPasswordPbkdf2(password, data.Salt, data.Iterations);

        if (!FixedTimeEquals(expected, data.PasswordHash))
            return false;

        if (isLegacySha256)
        {
            var upgraded = CreatePasswordData(password);
            await _store.SavePasswordDataAsync(upgraded, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<bool> HasPasswordAsync(CancellationToken cancellationToken = default)
    {
        return await _store.LoadPasswordDataAsync(cancellationToken).ConfigureAwait(false) != null;
    }

    public async Task<bool> ChangePasswordAsync(
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!await VerifyPasswordAsync(oldPassword, cancellationToken).ConfigureAwait(false))
                return false;

            if (await IsPasswordInHistoryAsync(newPassword, cancellationToken).ConfigureAwait(false))
                return false;

            var data = CreatePasswordData(newPassword);
            await _store.SavePasswordDataAsync(data, cancellationToken).ConfigureAwait(false);
            await AddToPasswordHistoryAsync(data.PasswordHash, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public async Task<string> EnableTwoFactorAuthAsync(
        string? secret = null,
        DateTime? enabledTime = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedSecret = string.IsNullOrWhiteSpace(secret) ? PortableTotp.GenerateSecret() : secret;
        await _store.SaveTwoFactorDataAsync(new PortableTwoFactorAuthData
        {
            Secret = resolvedSecret,
            IsEnabled = true,
            EnabledTime = enabledTime ?? _clock()
        }, cancellationToken).ConfigureAwait(false);
        return resolvedSecret;
    }

    public async Task DisableTwoFactorAuthAsync(CancellationToken cancellationToken = default)
    {
        var data = await _store.LoadTwoFactorDataAsync(cancellationToken).ConfigureAwait(false);
        if (data == null)
            return;

        data.IsEnabled = false;
        await _store.SaveTwoFactorDataAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsTwoFactorEnabledAsync(CancellationToken cancellationToken = default)
    {
        var data = await _store.LoadTwoFactorDataAsync(cancellationToken).ConfigureAwait(false);
        return data?.IsEnabled ?? false;
    }

    public async Task<string?> GetTwoFactorSecretAsync(CancellationToken cancellationToken = default)
    {
        var data = await _store.LoadTwoFactorDataAsync(cancellationToken).ConfigureAwait(false);
        return data?.Secret;
    }

    public async Task<bool> VerifyTotpCodeAsync(
        string code,
        long? timeStep = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _store.LoadTwoFactorDataAsync(cancellationToken).ConfigureAwait(false);
        if (data == null || !data.IsEnabled || string.IsNullOrEmpty(data.Secret))
            return false;

        var currentStep = timeStep ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        for (var offset = -1; offset <= 1; offset++)
        {
            if (string.Equals(code, PortableTotp.GenerateCode(data.Secret, currentStep + offset), StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private PortablePasswordData CreatePasswordData(string password)
    {
        var salt = GenerateSalt();
        return new PortablePasswordData
        {
            PasswordHash = HashPasswordPbkdf2(password, salt, DefaultIterations),
            Salt = salt,
            Iterations = DefaultIterations,
            HashAlgorithm = "PBKDF2",
            LastModifiedTime = _clock()
        };
    }

    private async Task AddToPasswordHistoryAsync(string hash, CancellationToken cancellationToken)
    {
        var history = (await _store.LoadPasswordHistoryAsync(cancellationToken).ConfigureAwait(false)).ToList();
        history.Add(hash);
        if (history.Count > 5)
            history = history.Skip(history.Count - 5).ToList();

        await _store.SavePasswordHistoryAsync(history, cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> IsPasswordInHistoryAsync(string password, CancellationToken cancellationToken)
    {
        var data = await _store.LoadPasswordDataAsync(cancellationToken).ConfigureAwait(false);
        if (data == null)
            return false;

        var history = await _store.LoadPasswordHistoryAsync(cancellationToken).ConfigureAwait(false);
        var hash = HashPasswordPbkdf2(password, data.Salt, data.Iterations);
        return history.Contains(hash);
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPasswordPbkdf2(string password, string salt, int iterations)
    {
        var saltBytes = Convert.FromBase64String(salt);
        using var pbkdf2 = new Rfc2898DeriveBytes(password, saltBytes, iterations, HashAlgorithmName.SHA256);
        return Convert.ToBase64String(pbkdf2.GetBytes(32));
    }

    private static string HashPasswordSha256(string password, string salt)
    {
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
    }
}

public sealed class PortablePasswordChangeController
{
    private readonly PortableAccountSecurityService _securityService;
    private readonly IPortablePasswordChangeApi _api;
    private readonly Func<string, (bool IsValid, string Message)> _passwordValidator;

    public PortablePasswordChangeController(
        PortableAccountSecurityService securityService,
        IPortablePasswordChangeApi api,
        Func<string, (bool IsValid, string Message)>? passwordValidator = null)
    {
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _passwordValidator = passwordValidator ?? PortablePasswordStrengthPolicy.ValidatePassword;
    }

    public async Task<PortablePasswordChangeResult> ChangePasswordAsync(
        string oldPassword,
        string newPassword,
        string confirmPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(oldPassword))
                return Warning("请输入旧密码");

            if (string.IsNullOrEmpty(newPassword))
                return Warning("请输入新密码");

            if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
                return Error("两次输入的密码不一致");

            var validation = _passwordValidator(newPassword);
            if (!validation.IsValid)
                return Error(validation.Message);

            if (!await _securityService.HasPasswordAsync(cancellationToken).ConfigureAwait(false))
            {
                await _securityService.SetInitialPasswordAsync(newPassword, cancellationToken).ConfigureAwait(false);
                return Success("设置密码", "密码设置成功");
            }

            if (!await _securityService.VerifyPasswordAsync(oldPassword, cancellationToken).ConfigureAwait(false))
                return Error("旧密码验证失败");

            var apiResult = await _api.ChangePasswordAsync(oldPassword, newPassword, cancellationToken).ConfigureAwait(false);
            if (!apiResult.Success)
            {
                return Error(string.Equals(apiResult.ErrorCode, "NETWORK_ERROR", StringComparison.OrdinalIgnoreCase)
                    ? "网络连接失败，请检查网络后重试"
                    : apiResult.Message ?? "服务器同步失败");
            }

            var changed = await _securityService.ChangePasswordAsync(oldPassword, newPassword, cancellationToken).ConfigureAwait(false);
            return changed
                ? Success("修改密码", "密码修改成功")
                : Error("新密码与历史密码重复");
        }
        catch (Exception ex)
        {
            return Error($"操作失败: {ex.Message}");
        }
    }

    private static PortablePasswordChangeResult Success(string title, string message)
    {
        return new PortablePasswordChangeResult
        {
            Success = true,
            Title = title,
            Message = message,
            ClearPasswordFields = true,
            Presentation = PortablePasswordChangePresentation.Success
        };
    }

    private static PortablePasswordChangeResult Warning(string message)
    {
        return new PortablePasswordChangeResult
        {
            Success = false,
            Message = message,
            Presentation = PortablePasswordChangePresentation.Warning
        };
    }

    private static PortablePasswordChangeResult Error(string message)
    {
        return new PortablePasswordChangeResult
        {
            Success = false,
            Message = message,
            Presentation = PortablePasswordChangePresentation.Error
        };
    }
}

public sealed class PortableTwoFactorController
{
    private readonly PortableAccountSecurityService _securityService;
    private readonly Func<PortableAccountSecurityService, string>? _secretFactory;
    private readonly Func<long>? _timeStepProvider;

    public PortableTwoFactorController(
        PortableAccountSecurityService securityService,
        Func<PortableAccountSecurityService, string>? secretFactory = null,
        Func<long>? timeStepProvider = null)
    {
        _securityService = securityService ?? throw new ArgumentNullException(nameof(securityService));
        _secretFactory = secretFactory;
        _timeStepProvider = timeStepProvider;
    }

    public async Task<PortableTwoFactorResult> EnableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var secret = _secretFactory?.Invoke(_securityService);
            var enabledSecret = await _securityService.EnableTwoFactorAuthAsync(secret, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return new PortableTwoFactorResult
            {
                Success = true,
                Title = "双因素认证",
                Message = "已启用双因素认证，请扫描二维码",
                Secret = enabledSecret,
                FormattedSecret = PortableTotp.FormatSecret(enabledSecret),
                TotpUri = PortableTotp.GenerateTotpUri(enabledSecret, "天命用户"),
                Presentation = PortableTwoFactorPresentation.Success
            };
        }
        catch (Exception ex)
        {
            return Error("双因素认证", $"启用失败: {ex.Message}");
        }
    }

    public async Task<PortableTwoFactorResult> DisableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _securityService.DisableTwoFactorAuthAsync(cancellationToken).ConfigureAwait(false);
            return new PortableTwoFactorResult
            {
                Success = true,
                Title = "双因素认证",
                Message = "已禁用双因素认证",
                Secret = string.Empty,
                FormattedSecret = string.Empty,
                TotpUri = null,
                Presentation = PortableTwoFactorPresentation.Info
            };
        }
        catch (Exception ex)
        {
            return Error("双因素认证", $"禁用失败: {ex.Message}");
        }
    }

    public async Task<PortableTwoFactorResult> VerifyCodeAsync(
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(verificationCode))
            {
                return new PortableTwoFactorResult
                {
                    Success = false,
                    Title = "验证码验证",
                    Message = "请输入验证码",
                    Presentation = PortableTwoFactorPresentation.Warning
                };
            }

            var valid = await _securityService.VerifyTotpCodeAsync(
                verificationCode,
                _timeStepProvider?.Invoke(),
                cancellationToken).ConfigureAwait(false);
            return new PortableTwoFactorResult
            {
                Success = valid,
                Title = "验证码验证",
                Message = valid ? "验证成功" : "验证码错误",
                ClearVerificationCode = true,
                Presentation = valid ? PortableTwoFactorPresentation.Success : PortableTwoFactorPresentation.Error
            };
        }
        catch (Exception ex)
        {
            return Error("验证码验证", $"操作失败: {ex.Message}");
        }
    }

    private static PortableTwoFactorResult Error(string title, string message)
    {
        return new PortableTwoFactorResult
        {
            Success = false,
            Title = title,
            Message = message,
            Presentation = PortableTwoFactorPresentation.Error
        };
    }
}

public static class PortableTotp
{
    private const string Base32Chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string GenerateSecret()
    {
        var bytes = new byte[20];
        RandomNumberGenerator.Fill(bytes);
        return ToBase32String(bytes);
    }

    public static string GenerateCode(string secret, long timeStep)
    {
        var counterBytes = BitConverter.GetBytes(timeStep);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(counterBytes);

        var secretBytes = FromBase32String(secret);
        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counterBytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
                     | ((hash[offset + 1] & 0xFF) << 16)
                     | ((hash[offset + 2] & 0xFF) << 8)
                     | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6");
    }

    public static string GenerateTotpUri(string secret, string accountName, string issuer = "天命")
    {
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(accountName)}?secret={secret}&issuer={Uri.EscapeDataString(issuer)}";
    }

    public static string FormatSecret(string secret)
    {
        if (string.IsNullOrEmpty(secret))
            return string.Empty;

        var result = new StringBuilder();
        for (var i = 0; i < secret.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
                result.Append(' ');
            result.Append(secret[i]);
        }

        return result.ToString();
    }

    private static string ToBase32String(byte[] bytes)
    {
        var result = new StringBuilder();
        var bits = 0;
        var bitsRemaining = 0;

        foreach (var b in bytes)
        {
            bits = (bits << 8) | b;
            bitsRemaining += 8;

            while (bitsRemaining >= 5)
            {
                var index = (bits >> (bitsRemaining - 5)) & 0x1F;
                result.Append(Base32Chars[index]);
                bitsRemaining -= 5;
            }
        }

        if (bitsRemaining > 0)
        {
            var index = (bits << (5 - bitsRemaining)) & 0x1F;
            result.Append(Base32Chars[index]);
        }

        return result.ToString();
    }

    private static byte[] FromBase32String(string base32)
    {
        var bits = 0;
        var bitsRemaining = 0;
        var result = new List<byte>();

        foreach (var c in base32.TrimEnd('=').ToUpperInvariant())
        {
            var value = Base32Chars.IndexOf(c);
            if (value < 0)
                continue;

            bits = (bits << 5) | value;
            bitsRemaining += 5;

            if (bitsRemaining >= 8)
            {
                result.Add((byte)(bits >> (bitsRemaining - 8)));
                bitsRemaining -= 8;
            }
        }

        return result.ToArray();
    }
}
