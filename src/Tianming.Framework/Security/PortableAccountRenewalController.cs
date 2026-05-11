namespace TM.Framework.Security;

public enum PortableAccountRenewalField
{
    None,
    Account,
    CardKey
}

public sealed class PortableAccountRenewalResult
{
    public bool Success { get; init; }
    public string? Account { get; init; }
    public int DaysAdded { get; init; }
    public string? Message { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }
    public PortableAccountRenewalField FocusField { get; init; } = PortableAccountRenewalField.None;
}

public interface IPortableAccountRenewalApi
{
    Task<PortableApiResponse<PortableActivationResult>> RenewAccountWithCardKeyAsync(
        string account,
        string cardKey,
        CancellationToken cancellationToken = default);
}

public sealed class PortableAccountRenewalController
{
    private readonly IPortableAccountRenewalApi _api;

    public PortableAccountRenewalController(IPortableAccountRenewalApi api)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public async Task<PortableAccountRenewalResult> RenewAsync(
        string? account,
        string? cardKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedAccount = (account ?? string.Empty).Trim();
        var normalizedCardKey = (cardKey ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedAccount))
        {
            return Fail("请输入账号", PortableAccountRenewalField.Account);
        }

        if (string.IsNullOrWhiteSpace(normalizedCardKey))
        {
            return Fail("请输入卡密", PortableAccountRenewalField.CardKey);
        }

        try
        {
            var response = await _api.RenewAccountWithCardKeyAsync(
                normalizedAccount,
                normalizedCardKey,
                cancellationToken).ConfigureAwait(false);

            if (response.Success && response.Data != null)
            {
                return new PortableAccountRenewalResult
                {
                    Success = true,
                    Account = normalizedAccount,
                    DaysAdded = response.Data.DaysAdded,
                    Message = $"续费成功！已为账号增加 {response.Data.DaysAdded} 天会员时长"
                };
            }

            return Fail(
                string.IsNullOrWhiteSpace(response.Message) ? "续费失败" : response.Message,
                PortableAccountRenewalField.None,
                response.ErrorCode);
        }
        catch (Exception ex)
        {
            return Fail($"续费失败: {ex.Message}", PortableAccountRenewalField.None);
        }
    }

    private static PortableAccountRenewalResult Fail(
        string message,
        PortableAccountRenewalField focusField,
        string? errorCode = null)
    {
        return new PortableAccountRenewalResult
        {
            Success = false,
            ErrorMessage = message,
            ErrorCode = errorCode,
            FocusField = focusField
        };
    }
}
