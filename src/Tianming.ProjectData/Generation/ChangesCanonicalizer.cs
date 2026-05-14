using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ChangesCanonicalizer
    {
        private static readonly (string Chinese, string English)[] CanonicalOrder =
        {
            ("角色状态变化", "CharacterStateChanges"),
            ("冲突进度", "ConflictProgress"),
            ("伏笔动作", "ForeshadowingActions"),
            ("新增剧情", "NewPlotPoints"),
            ("地点状态变化", "LocationStateChanges"),
            ("势力状态变化", "FactionStateChanges"),
            ("时间推进", "TimeProgression"),
            ("角色移动", "CharacterMovements"),
            ("物品流转", "ItemTransfers"),
        };

        public static string Canonicalize(string rawJson)
        {
            if (string.IsNullOrWhiteSpace(rawJson))
            {
                return "{}";
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(rawJson);
            }
            catch (JsonException)
            {
                return rawJson;
            }

            if (root is not JsonObject obj)
            {
                return rawJson;
            }

            var useEnglishKeys = ShouldUseEnglishKeys(obj);
            var knownKeys = new HashSet<string>(StringComparer.Ordinal);
            var canon = new JsonObject();

            foreach (var (chinese, english) in CanonicalOrder)
            {
                knownKeys.Add(chinese);
                knownKeys.Add(english);

                var outputKey = useEnglishKeys ? english : chinese;
                if (TryGetCanonicalValue(obj, chinese, english, out var value))
                {
                    canon[outputKey] = value?.DeepClone();
                }
                else
                {
                    canon[outputKey] = outputKey is "时间推进" or "TimeProgression" ? null : new JsonArray();
                }
            }

            foreach (var kv in obj)
            {
                if (!knownKeys.Contains(kv.Key))
                {
                    canon[kv.Key] = kv.Value?.DeepClone();
                }
            }

            return canon.ToJsonString(new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                WriteIndented = true
            });
        }

        private static bool TryGetCanonicalValue(JsonObject obj, string chinese, string english, out JsonNode? value)
        {
            if (obj.TryGetPropertyValue(chinese, out value))
            {
                return true;
            }

            if (obj.TryGetPropertyValue(english, out value))
            {
                return true;
            }

            value = null;
            return false;
        }

        private static bool ShouldUseEnglishKeys(JsonObject obj)
        {
            var englishHits = 0;
            var chineseHits = 0;

            foreach (var (chinese, english) in CanonicalOrder)
            {
                if (obj.ContainsKey(english))
                {
                    englishHits++;
                }

                if (obj.ContainsKey(chinese))
                {
                    chineseHits++;
                }
            }

            return englishHits > 0 && englishHits >= chineseHits;
        }
    }
}
