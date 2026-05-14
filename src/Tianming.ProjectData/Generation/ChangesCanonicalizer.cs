using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ChangesCanonicalizer
    {
        private static readonly string[] CanonicalOrder =
        {
            "CharacterStateChanges",
            "ConflictProgress",
            "ForeshadowingActions",
            "NewPlotPoints",
            "LocationStateChanges",
            "FactionStateChanges",
            "TimeProgression",
            "CharacterMovements",
            "ItemTransfers",
        };

        private static readonly Dictionary<string, string> KnownAliasMap = new(StringComparer.Ordinal)
        {
            ["角色状态变化"] = "CharacterStateChanges",
            ["角色状态变更"] = "CharacterStateChanges",
            ["角色变化"] = "CharacterStateChanges",
            ["冲突进度"] = "ConflictProgress",
            ["冲突进展"] = "ConflictProgress",
            ["伏笔动作"] = "ForeshadowingActions",
            ["伏笔操作"] = "ForeshadowingActions",
            ["新增情节"] = "NewPlotPoints",
            ["新情节点"] = "NewPlotPoints",
            ["新增剧情"] = "NewPlotPoints",
            ["地点状态变化"] = "LocationStateChanges",
            ["地点状态变更"] = "LocationStateChanges",
            ["地点变化"] = "LocationStateChanges",
            ["势力状态变化"] = "FactionStateChanges",
            ["势力状态变更"] = "FactionStateChanges",
            ["势力变化"] = "FactionStateChanges",
            ["时间推进"] = "TimeProgression",
            ["时间进展"] = "TimeProgression",
            ["角色移动"] = "CharacterMovements",
            ["角色位移"] = "CharacterMovements",
            ["物品流转"] = "ItemTransfers",
            ["物品转移"] = "ItemTransfers",
            ["道具流转"] = "ItemTransfers",
            ["角色ID"] = "CharacterId",
            ["角色编号"] = "CharacterId",
            ["新等级"] = "NewLevel",
            ["新能力"] = "NewAbilities",
            ["失去能力"] = "LostAbilities",
            ["关系变化"] = "RelationshipChanges",
            ["字段变化"] = "FieldChanges",
            ["新心理状态"] = "NewMentalState",
            ["心理状态"] = "NewMentalState",
            ["关键事件"] = "KeyEvent",
            ["重要性"] = "Importance",
            ["冲突ID"] = "ConflictId",
            ["冲突编号"] = "ConflictId",
            ["新状态"] = "NewStatus",
            ["事件"] = "Event",
            ["伏笔ID"] = "ForeshadowId",
            ["伏笔编号"] = "ForeshadowId",
            ["动作"] = "Action",
            ["关键词"] = "Keywords",
            ["上下文"] = "Context",
            ["涉及角色"] = "InvolvedCharacters",
            ["故事线"] = "Storyline",
            ["地点ID"] = "LocationId",
            ["地点编号"] = "LocationId",
            ["势力ID"] = "FactionId",
            ["势力编号"] = "FactionId",
            ["时间段"] = "TimePeriod",
            ["经过时间"] = "ElapsedTime",
            ["关键时间事件"] = "KeyTimeEvent",
            ["出发地"] = "FromLocation",
            ["目的地"] = "ToLocation",
            ["物品ID"] = "ItemId",
            ["物品编号"] = "ItemId",
            ["物品名称"] = "ItemName",
            ["原持有者"] = "FromHolder",
            ["新持有者"] = "ToHolder",
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

            if (NormalizeKnownAliases(root) is not JsonObject obj)
            {
                return rawJson;
            }

            var knownKeys = new HashSet<string>(StringComparer.Ordinal);
            var canon = new JsonObject();

            foreach (var field in CanonicalOrder)
            {
                knownKeys.Add(field);
                if (obj.TryGetPropertyValue(field, out var value))
                {
                    canon[field] = value?.DeepClone();
                }
                else
                {
                    canon[field] = field == "TimeProgression" ? null : new JsonArray();
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

        private static JsonNode? NormalizeKnownAliases(JsonNode? node)
        {
            if (node is JsonObject obj)
            {
                var normalized = new JsonObject();
                foreach (var kv in obj)
                {
                    var canonicalKey = KnownAliasMap.TryGetValue(kv.Key, out var mappedKey) ? mappedKey : kv.Key;
                    var normalizedValue = NormalizeKnownAliases(kv.Value);
                    if (!normalized.ContainsKey(canonicalKey) || string.Equals(kv.Key, canonicalKey, StringComparison.Ordinal))
                    {
                        normalized[canonicalKey] = normalizedValue;
                    }
                }

                return normalized;
            }

            if (node is JsonArray array)
            {
                var normalizedArray = new JsonArray();
                foreach (var item in array)
                {
                    normalizedArray.Add(NormalizeKnownAliases(item));
                }

                return normalizedArray;
            }

            return node?.DeepClone();
        }
    }
}
