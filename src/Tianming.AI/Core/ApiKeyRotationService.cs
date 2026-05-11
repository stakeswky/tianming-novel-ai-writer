using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace TM.Services.Framework.AI.Core;

public class ApiKeyRotationService
{
    private readonly ConcurrentDictionary<string, KeyPool> _pools = new();
    private readonly Action<string>? _log;

    public ApiKeyRotationService(Action<string>? log = null)
    {
        _log = log;
    }

    public event Action<string>? KeyStateChanged;

    public void UpdateKeyPool(string providerId, List<ApiKeyEntry> keys)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return;

        var pool = _pools.GetOrAdd(providerId, _ => new KeyPool());
        lock (pool)
        {
            pool.Keys = keys?
                .Where(key => !string.IsNullOrWhiteSpace(key.Key))
                .ToList() ?? new List<ApiKeyEntry>();

            var validIds = new HashSet<string>(pool.Keys.Select(key => key.Id));
            foreach (var id in pool.HealthMap.Keys.ToList())
            {
                if (!validIds.Contains(id))
                    pool.HealthMap.TryRemove(id, out _);
            }
        }
    }

    public KeySelection? GetNextKey(string providerId)
    {
        return GetNextKey(providerId, null);
    }

    public KeySelection? GetNextKey(string providerId, HashSet<string>? excludeKeyIds)
    {
        if (string.IsNullOrWhiteSpace(providerId)) return null;
        if (!_pools.TryGetValue(providerId, out var pool)) return null;

        List<ApiKeyEntry> candidates;
        lock (pool)
        {
            var now = DateTime.UtcNow;
            candidates = pool.Keys
                .Where(key => key.IsEnabled
                    && !string.IsNullOrWhiteSpace(key.Key)
                    && (excludeKeyIds == null || !excludeKeyIds.Contains(key.Id))
                    && !IsTemporarilyDisabled(pool, key.Id, now))
                .ToList();
        }

        if (candidates.Count == 0) return null;

        var index = Interlocked.Increment(ref pool.CurrentIndex);
        var safeIndex = (index & int.MaxValue) % candidates.Count;
        var selected = candidates[safeIndex];

        return new KeySelection(selected.Id, selected.Key, selected.Remark);
    }

    public void ReportKeyResult(
        string providerId,
        string keyId,
        KeyUseResult result,
        string? rawErrorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(providerId) || string.IsNullOrWhiteSpace(keyId)) return;
        if (!_pools.TryGetValue(providerId, out var pool)) return;

        var health = pool.HealthMap.GetOrAdd(keyId, _ => new KeyHealth());

        lock (health)
        {
            health.TotalRequests++;

            switch (result)
            {
                case KeyUseResult.Success:
                    health.ConsecutiveFailures = 0;
                    health.LastFailureReason = null;
                    break;

                case KeyUseResult.AuthFailure:
                case KeyUseResult.Forbidden:
                case KeyUseResult.QuotaExhausted:
                    health.TotalFailures++;
                    health.ConsecutiveFailures++;
                    health.LastFailureReason = result;
                    health.LastErrorMessage = rawErrorMessage;
                    PermanentlyDisableKey(pool, providerId, keyId);
                    _log?.Invoke($"[ApiKeyRotation] 永久禁用密钥 {keyId}: {result} - {rawErrorMessage}");
                    break;

                case KeyUseResult.RateLimited:
                    health.TotalFailures++;
                    health.LastFailureReason = result;
                    health.LastErrorMessage = rawErrorMessage;
                    health.DisabledUntil = DateTime.UtcNow.AddSeconds(60);
                    _log?.Invoke($"[ApiKeyRotation] 临时禁用密钥 {keyId} 60秒: RateLimited");
                    break;

                case KeyUseResult.ServerError:
                    health.TotalFailures++;
                    health.ConsecutiveFailures++;
                    health.LastFailureReason = result;
                    health.LastErrorMessage = rawErrorMessage;
                    if (health.ConsecutiveFailures >= 5)
                    {
                        health.DisabledUntil = DateTime.UtcNow.AddMinutes(5);
                        _log?.Invoke($"[ApiKeyRotation] 临时禁用密钥 {keyId} 5分钟: 连续失败 {health.ConsecutiveFailures} 次");
                    }
                    else if (health.ConsecutiveFailures >= 3)
                    {
                        health.DisabledUntil = DateTime.UtcNow.AddSeconds(60);
                        _log?.Invoke($"[ApiKeyRotation] 临时禁用密钥 {keyId} 60秒: 连续失败 {health.ConsecutiveFailures} 次");
                    }
                    break;

                case KeyUseResult.NetworkError:
                    break;

                default:
                    health.TotalFailures++;
                    health.ConsecutiveFailures++;
                    health.LastFailureReason = result;
                    health.LastErrorMessage = rawErrorMessage;
                    break;
            }
        }
    }

    public void SetRateLimitCooldown(string providerId, string keyId, int seconds)
    {
        if (!_pools.TryGetValue(providerId, out var pool)) return;

        var health = pool.HealthMap.GetOrAdd(keyId, _ => new KeyHealth());
        lock (health)
        {
            health.DisabledUntil = DateTime.UtcNow.AddSeconds(Math.Max(seconds, 1));
        }
    }

    public KeyPoolStatus? GetPoolStatus(string providerId)
    {
        if (!_pools.TryGetValue(providerId, out var pool)) return null;

        lock (pool)
        {
            var now = DateTime.UtcNow;
            var entries = pool.Keys.Select(key =>
            {
                pool.HealthMap.TryGetValue(key.Id, out var health);
                var status = !key.IsEnabled ? KeyEntryStatus.PermanentlyDisabled
                    : health != null && IsHealthDisabled(health, now) ? KeyEntryStatus.TemporarilyDisabled
                    : KeyEntryStatus.Active;

                return new KeyEntryStatusInfo(
                    key.Id,
                    key.Remark,
                    status,
                    health?.LastFailureReason,
                    health?.LastErrorMessage,
                    health?.TotalRequests ?? 0,
                    health?.TotalFailures ?? 0,
                    health?.DisabledUntil);
            }).ToList();

            return new KeyPoolStatus(
                pool.Keys.Count,
                entries.Count(entry => entry.Status == KeyEntryStatus.Active),
                entries);
        }
    }

    private static bool IsTemporarilyDisabled(KeyPool pool, string keyId, DateTime now)
    {
        if (!pool.HealthMap.TryGetValue(keyId, out var health)) return false;
        return IsHealthDisabled(health, now);
    }

    private static bool IsHealthDisabled(KeyHealth health, DateTime now)
    {
        if (health.DisabledUntil.HasValue && health.DisabledUntil.Value > now)
            return true;

        if (health.DisabledUntil.HasValue)
            health.DisabledUntil = null;

        return false;
    }

    private void PermanentlyDisableKey(KeyPool pool, string providerId, string keyId)
    {
        lock (pool)
        {
            var key = pool.Keys.FirstOrDefault(item => item.Id == keyId);
            if (key != null)
                key.IsEnabled = false;
        }

        KeyStateChanged?.Invoke(providerId);
    }

    private class KeyPool
    {
        public List<ApiKeyEntry> Keys { get; set; } = new();
        public int CurrentIndex;
        public ConcurrentDictionary<string, KeyHealth> HealthMap { get; } = new();
    }

    private class KeyHealth
    {
        public int ConsecutiveFailures { get; set; }
        public DateTime? DisabledUntil { get; set; }
        public long TotalRequests { get; set; }
        public long TotalFailures { get; set; }
        public KeyUseResult? LastFailureReason { get; set; }
        public string? LastErrorMessage { get; set; }
    }
}

public enum KeyEntryStatus
{
    Active,
    TemporarilyDisabled,
    PermanentlyDisabled
}

public record KeyEntryStatusInfo(
    string KeyId,
    string? Remark,
    KeyEntryStatus Status,
    KeyUseResult? LastFailureReason,
    string? LastErrorMessage,
    long TotalRequests,
    long TotalFailures,
    DateTime? DisabledUntil);

public record KeyPoolStatus(
    int TotalKeys,
    int ActiveKeys,
    List<KeyEntryStatusInfo> Entries);
