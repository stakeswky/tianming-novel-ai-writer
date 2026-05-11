using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations;

public class ChangesProtocolParser
{
    public const string ChangesSeparator = "---CHANGES---";

    private static readonly Regex ChangesSeparatorLineRegex = new(
        @"(?m)^\s*[-\u2010\u2011\u2012\u2013\u2014\u2212]{3}\s*CHANGES\s*[-\u2010\u2011\u2012\u2013\u2014\u2212]{3}\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MdChangesHeaderRegex = new(
        @"(?m)^(?:---\s*\n+\s*)?#{1,3}\s*(?:CHANGES|变更记录|变更摘要|状态变更)\s*\n",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] ChangesSignatureFields =
    {
        "CharacterStateChanges", "ConflictProgress", "ForeshadowingActions",
        "NewPlotPoints", "LocationStateChanges", "FactionStateChanges",
        "TimeProgression", "CharacterMovements", "ItemTransfers"
    };

    private static readonly string[] RequiredFields =
    {
        "CharacterStateChanges",
        "ConflictProgress",
        "ForeshadowingActions",
        "NewPlotPoints",
        "LocationStateChanges",
        "FactionStateChanges",
        "TimeProgression",
        "CharacterMovements",
        "ItemTransfers"
    };

    public ProtocolValidationResult ValidateChangesProtocol(string rawContent)
    {
        var result = new ProtocolValidationResult();

        if (string.IsNullOrEmpty(rawContent))
        {
            result.AddError("内容为空");
            return result;
        }

        var (contentPart, changesPart, formatType) = IdentifyChangesRegion(rawContent);

        if (changesPart == null)
        {
            result.AddError($"未识别到CHANGES区域（支持格式：{ChangesSeparator}、末尾JSON）");
            return result;
        }

        result.ContentWithoutChanges = contentPart;

        var (changes, parseError) = ParseChangesContent(changesPart, formatType);
        if (changes == null)
        {
            result.AddError(parseError ?? "CHANGES解析失败");
            return result;
        }

        result.Changes = changes;

        if (formatType == ChangesFormatType.Json || formatType == ChangesFormatType.JsonNoSeparator)
        {
            var jsonStr = ExtractJsonFromChangesSection(changesPart);
            if (!string.IsNullOrEmpty(jsonStr))
            {
                ValidateRequiredFields(result, RepairChangesJson(jsonStr));
            }
        }

        if (!result.HasErrors)
        {
            ValidateShortIdFields(result);
        }

        result.Success = !result.HasErrors;
        return result;
    }

    private enum ChangesFormatType
    {
        Json,
        JsonNoSeparator,
        Markdown
    }

    private static (string content, string? changes, ChangesFormatType format) IdentifyChangesRegion(string rawContent)
    {
        var idx = rawContent.IndexOf(ChangesSeparator, StringComparison.Ordinal);
        if (idx >= 0)
        {
            return (
                rawContent[..idx].Trim(),
                rawContent[(idx + ChangesSeparator.Length)..].Trim(),
                ChangesFormatType.Json
            );
        }

        var separatorMatch = ChangesSeparatorLineRegex.Match(rawContent);
        if (separatorMatch.Success)
        {
            return (
                rawContent[..separatorMatch.Index].Trim(),
                rawContent[(separatorMatch.Index + separatorMatch.Length)..].Trim(),
                ChangesFormatType.Json
            );
        }

        var markdownMatch = MdChangesHeaderRegex.Match(rawContent);
        if (markdownMatch.Success)
        {
            return (
                rawContent[..markdownMatch.Index].Trim(),
                rawContent[markdownMatch.Index..].Trim(),
                ChangesFormatType.Markdown
            );
        }

        var jsonResult = TryIdentifyTrailingJson(rawContent);
        if (jsonResult.HasValue)
        {
            return (
                rawContent[..jsonResult.Value.startIndex].Trim(),
                jsonResult.Value.json,
                ChangesFormatType.JsonNoSeparator
            );
        }

        var markdownContentMatch = Regex.Match(rawContent, @"(?m)^---\s*\n+\s*\*\*[^*]+\*\*[：:]");
        if (markdownContentMatch.Success)
        {
            return (
                rawContent[..markdownContentMatch.Index].Trim(),
                rawContent[markdownContentMatch.Index..].Trim(),
                ChangesFormatType.Markdown
            );
        }

        return (rawContent, null, ChangesFormatType.Json);
    }

    private static (int startIndex, string json)? TryIdentifyTrailingJson(string rawContent)
    {
        var lastBrace = rawContent.LastIndexOf('}');
        if (lastBrace < 0) return null;

        var braceCount = 0;
        var jsonStartIndex = -1;

        for (var i = lastBrace; i >= 0; i--)
        {
            var c = rawContent[i];
            if (c == '}') braceCount++;
            else if (c == '{')
            {
                braceCount--;
                if (braceCount == 0)
                {
                    jsonStartIndex = i;
                    break;
                }
            }
        }

        if (jsonStartIndex < 0) return null;

        var candidateJson = rawContent.Substring(jsonStartIndex, lastBrace - jsonStartIndex + 1);

        try
        {
            using var doc = JsonDocument.Parse(candidateJson, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });

            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            var matchedFields = 0;
            foreach (var field in ChangesSignatureFields)
            {
                if (doc.RootElement.TryGetProperty(field, out _) ||
                    doc.RootElement.TryGetProperty(ToCamelCase(field), out _))
                {
                    matchedFields++;
                }
            }

            if (matchedFields < 2) return null;

            var actualStart = jsonStartIndex;
            var beforeJson = rawContent[..jsonStartIndex].TrimEnd();
            var codeBlockIdx = beforeJson.LastIndexOf("```", StringComparison.Ordinal);
            if (codeBlockIdx >= 0)
            {
                var between = beforeJson[(codeBlockIdx + 3)..].Trim();
                if (string.IsNullOrEmpty(between) || between.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var lineStart = beforeJson.LastIndexOf('\n', codeBlockIdx);
                    actualStart = lineStart >= 0 ? lineStart : codeBlockIdx;
                }
            }

            return (actualStart, candidateJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ToCamelCase(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static (ChapterChanges? changes, string? error) ParseChangesContent(string changesPart, ChangesFormatType formatType)
    {
        if (formatType == ChangesFormatType.Markdown)
        {
            return (null, "CHANGES只允许JSON格式（### CHANGES 等 Markdown 格式不被接受）。请确保正文末尾输出 ---CHANGES--- 分隔符并紧跟JSON对象。");
        }

        var jsonStr = ExtractJsonFromChangesSection(changesPart);
        if (!string.IsNullOrEmpty(jsonStr))
        {
            var jsonResult = TryParseJsonChanges(jsonStr);
            if (jsonResult != null)
            {
                return (jsonResult, null);
            }
        }

        return (null, "CHANGES解析失败：JSON格式无法解析，请检查JSON语法（括号、逗号、引号）");
    }

    private static ChapterChanges? TryParseJsonChanges(string jsonStr)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };
            options.Converters.Add(new CharacterStateChangeConverter());

            try
            {
                return JsonSerializer.Deserialize<ChapterChanges>(jsonStr, options);
            }
            catch (JsonException)
            {
                var repaired = RepairChangesJson(jsonStr);
                if (!string.Equals(repaired, jsonStr, StringComparison.Ordinal))
                {
                    return JsonSerializer.Deserialize<ChapterChanges>(repaired, options);
                }

                throw;
            }
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ExtractJsonFromChangesSection(string changesSection)
    {
        if (string.IsNullOrEmpty(changesSection))
            return string.Empty;

        var content = changesSection.Trim();

        if (content.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewline = content.IndexOf('\n');
            if (firstNewline > 0)
            {
                content = content[(firstNewline + 1)..];
            }

            var lastBackticks = content.LastIndexOf("```", StringComparison.Ordinal);
            if (lastBackticks > 0)
            {
                content = content[..lastBackticks];
            }
        }

        content = content.Trim();

        var jsonStart = content.IndexOf('{');
        if (jsonStart < 0)
        {
            return content;
        }

        var best = string.Empty;
        var end = content.IndexOf('}', jsonStart + 1);
        while (end > jsonStart)
        {
            var candidate = content.Substring(jsonStart, end - jsonStart + 1);
            try
            {
                using var doc = JsonDocument.Parse(candidate, new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                });

                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return candidate;
                }
            }
            catch
            {
                best = candidate;
            }

            end = content.IndexOf('}', end + 1);
        }

        return string.IsNullOrEmpty(best)
            ? content[jsonStart..]
            : best;
    }

    private static string RepairChangesJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        json = RemoveJsonComments(json);
        json = MapChineseFieldNames(json);

        var sb = new StringBuilder(json.Length + 64);
        var inString = false;
        var stringQuoteChar = '"';
        var escape = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                if (escape)
                {
                    escape = false;
                    sb.Append(c);
                    continue;
                }

                if (c == '\\')
                {
                    escape = true;
                    sb.Append(c);
                    continue;
                }

                if (c == stringQuoteChar)
                {
                    inString = false;
                    sb.Append('"');
                    continue;
                }

                sb.Append(c);
                continue;
            }

            if (c == '\'')
            {
                inString = true;
                stringQuoteChar = '\'';
                sb.Append('"');
                continue;
            }

            if (c == '"')
            {
                inString = true;
                stringQuoteChar = '"';
                sb.Append('"');
                continue;
            }

            if (c == '：')
            {
                sb.Append(':');
                continue;
            }

            if (c == '，')
            {
                sb.Append(',');
                continue;
            }

            if (c == ',')
            {
                var j = i + 1;
                while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                if (j < json.Length && (json[j] == '}' || json[j] == ']'))
                {
                    continue;
                }
                sb.Append(c);
                continue;
            }

            if (c == '}')
            {
                sb.Append(c);
                var j = i + 1;
                while (j < json.Length && char.IsWhiteSpace(json[j])) j++;
                if (j < json.Length && json[j] == '{')
                {
                    sb.Append(',');
                }
                continue;
            }

            if (char.IsLetter(c) || c == '_')
            {
                var prevNonWhitespace = FindPrevNonWhitespace(json, i - 1);
                if (prevNonWhitespace == '{' || prevNonWhitespace == ',')
                {
                    var identEnd = i;
                    while (identEnd < json.Length && (char.IsLetterOrDigit(json[identEnd]) || json[identEnd] == '_'))
                        identEnd++;
                    var colonPos = identEnd;
                    while (colonPos < json.Length && char.IsWhiteSpace(json[colonPos]))
                        colonPos++;
                    if (colonPos < json.Length && (json[colonPos] == ':' || json[colonPos] == '：'))
                    {
                        var fieldName = json.Substring(i, identEnd - i);
                        if (ChineseFieldNameMap.TryGetValue(fieldName, out var mappedFieldName))
                            fieldName = mappedFieldName;
                        sb.Append('"').Append(fieldName).Append('"');
                        i = identEnd - 1;
                        continue;
                    }
                }
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static char FindPrevNonWhitespace(string value, int from)
    {
        for (var i = from; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(value[i]))
                return value[i];
        }
        return '\0';
    }

    private static string RemoveJsonComments(string json)
    {
        var sb = new StringBuilder(json.Length);
        var inString = false;
        var quoteChar = '"';
        var escape = false;

        for (var i = 0; i < json.Length; i++)
        {
            var c = json[i];

            if (inString)
            {
                sb.Append(c);
                if (escape)
                {
                    escape = false;
                    continue;
                }
                if (c == '\\')
                {
                    escape = true;
                    continue;
                }
                if (c == quoteChar) inString = false;
                continue;
            }

            if (c == '"' || c == '\'')
            {
                inString = true;
                quoteChar = c;
                sb.Append(c);
                continue;
            }

            if (c == '/' && i + 1 < json.Length && json[i + 1] == '/')
            {
                while (i < json.Length && json[i] != '\n') i++;
                if (i < json.Length) sb.Append('\n');
                continue;
            }

            if (c == '/' && i + 1 < json.Length && json[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < json.Length && !(json[i] == '*' && json[i + 1] == '/')) i++;
                if (i + 1 < json.Length) i++;
                continue;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    private static string MapChineseFieldNames(string json)
    {
        foreach (var kv in ChineseFieldNameMap)
        {
            if (json.Contains(kv.Key, StringComparison.Ordinal))
            {
                json = json.Replace($"\"{kv.Key}\"", $"\"{kv.Value}\"", StringComparison.Ordinal);
                json = json.Replace($"'{kv.Key}'", $"\"{kv.Value}\"", StringComparison.Ordinal);
            }
        }
        return json;
    }

    private static void ValidateRequiredFields(ProtocolValidationResult result, string jsonStr)
    {
        if (result.Changes == null)
        {
            result.AddError("CHANGES对象为空");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonStr);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                result.AddError("CHANGES JSON 不是对象");
                return;
            }

            foreach (var field in RequiredFields)
            {
                if (!HasPropertyIgnoreCase(doc.RootElement, field))
                {
                    result.AddError($"CHANGES缺失必需字段: {field}（必须显式声明，即使为空数组）");
                }
            }
        }
        catch (JsonException)
        {
            foreach (var field in RequiredFields)
            {
                if (!jsonStr.Contains($"\"{field}\"", StringComparison.OrdinalIgnoreCase))
                {
                    result.AddError($"CHANGES缺失必需字段: {field}（必须显式声明，即使为空数组）");
                }
            }
        }
    }

    private static bool HasPropertyIgnoreCase(JsonElement obj, string propertyName)
    {
        if (obj.ValueKind != JsonValueKind.Object)
            return false;

        foreach (var prop in obj.EnumerateObject())
        {
            if (string.Equals(prop.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ValidateShortIdFields(ProtocolValidationResult result)
    {
        var changes = result.Changes;
        if (changes == null) return;

        void Require(string field, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value) && !ShortIdGenerator.IsLikelyId(value))
                result.AddError($"CHANGES协议违规：{field} 必须为 ShortId（格式：13字符大写字母开头，如`D7M3VT2K9P4N`），收到非法值'{value}'");
        }

        foreach (var change in changes.CharacterStateChanges ?? new())
        {
            Require("CharacterStateChanges[].CharacterId", change.CharacterId);
            foreach (var key in change.RelationshipChanges?.Keys ?? Enumerable.Empty<string>())
                Require("CharacterStateChanges[].RelationshipChanges.key", key);
        }

        foreach (var progress in changes.ConflictProgress ?? new())
            Require("ConflictProgress[].ConflictId", progress.ConflictId);

        foreach (var action in changes.ForeshadowingActions ?? new())
            Require("ForeshadowingActions[].ForeshadowId", action.ForeshadowId);

        foreach (var location in changes.LocationStateChanges ?? new())
            Require("LocationStateChanges[].LocationId", location.LocationId);

        foreach (var faction in changes.FactionStateChanges ?? new())
            Require("FactionStateChanges[].FactionId", faction.FactionId);

        foreach (var movement in changes.CharacterMovements ?? new())
        {
            Require("CharacterMovements[].CharacterId", movement.CharacterId);
            if (!string.IsNullOrWhiteSpace(movement.FromLocation))
                Require("CharacterMovements[].FromLocation", movement.FromLocation);
            if (!string.IsNullOrWhiteSpace(movement.ToLocation))
                Require("CharacterMovements[].ToLocation", movement.ToLocation);
        }

        foreach (var transfer in changes.ItemTransfers ?? new())
        {
            Require("ItemTransfers[].ItemId", transfer.ItemId);
            if (!string.IsNullOrWhiteSpace(transfer.FromHolder))
                Require("ItemTransfers[].FromHolder", transfer.FromHolder);
            if (!string.IsNullOrWhiteSpace(transfer.ToHolder))
                Require("ItemTransfers[].ToHolder", transfer.ToHolder);
        }

        foreach (var plotPoint in changes.NewPlotPoints ?? new())
            foreach (var involvedCharacter in plotPoint.InvolvedCharacters ?? new())
                Require("NewPlotPoints[].InvolvedCharacters[]", involvedCharacter);
    }

    private sealed class CharacterStateChangeConverter : System.Text.Json.Serialization.JsonConverter<CharacterStateChange>
    {
        public override CharacterStateChange? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new CharacterStateChange();
            }

            var model = new CharacterStateChange();

            if (TryGetString(root, "CharacterId", out var characterId) || TryGetString(root, "characterId", out characterId))
                model.CharacterId = characterId;
            else if (TryGetString(root, "Character", out var character) || TryGetString(root, "character", out character))
                model.CharacterId = character;

            if (TryGetString(root, "NewLevel", out var newLevel) || TryGetString(root, "newLevel", out newLevel) || TryGetString(root, "level", out newLevel))
                model.NewLevel = newLevel;

            if (TryGetString(root, "NewMentalState", out var mental) || TryGetString(root, "newMentalState", out mental) || TryGetString(root, "mentalState", out mental))
                model.NewMentalState = mental;

            if (TryGetString(root, "KeyEvent", out var keyEvent) || TryGetString(root, "keyEvent", out keyEvent))
                model.KeyEvent = keyEvent;
            else if (TryGetString(root, "change", out var altKeyEvent) || TryGetString(root, "Change", out altKeyEvent))
                model.KeyEvent = altKeyEvent;

            ReadStringList(root, model.NewAbilities, "NewAbilities", "newAbilities", "abilities");
            ReadStringList(root, model.LostAbilities, "LostAbilities", "lostAbilities");

            if (TryGetString(root, "Importance", out var importance) || TryGetString(root, "importance", out importance))
                model.Importance = importance;

            if (root.TryGetProperty("RelationshipChanges", out var relEl) || root.TryGetProperty("relationshipChanges", out relEl))
            {
                if (relEl.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in relEl.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Object)
                        {
                            var relationship = new RelationshipChange();
                            if (TryGetString(prop.Value, "Relation", out var relation) || TryGetString(prop.Value, "relation", out relation))
                                relationship.Relation = relation;
                            if (TryGetInt(prop.Value, "TrustDelta", out var delta) || TryGetInt(prop.Value, "trustDelta", out delta))
                                relationship.TrustDelta = delta;
                            model.RelationshipChanges[prop.Name] = relationship;
                        }
                    }
                }
            }

            return model;
        }

        public override void Write(Utf8JsonWriter writer, CharacterStateChange value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }

        private static void ReadStringList(JsonElement root, List<string> target, params string[] propertyNames)
        {
            foreach (var propertyName in propertyNames)
            {
                if (!root.TryGetProperty(propertyName, out var element))
                    continue;

                if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            target.Add(item.GetString() ?? string.Empty);
                    }
                    return;
                }

                if (element.ValueKind == JsonValueKind.String)
                {
                    var value = element.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                        target.Add(value);
                    return;
                }
            }
        }

        private static bool TryGetString(JsonElement obj, string name, out string value)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
            {
                value = el.GetString() ?? string.Empty;
                return true;
            }
            value = string.Empty;
            return false;
        }

        private static bool TryGetInt(JsonElement obj, string name, out int value)
        {
            if (obj.ValueKind == JsonValueKind.Object && obj.TryGetProperty(name, out var el))
            {
                if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out value))
                    return true;
                if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out value))
                    return true;
            }
            value = 0;
            return false;
        }
    }

    private static readonly Dictionary<string, string> ChineseFieldNameMap = new(StringComparer.Ordinal)
    {
        { "角色状态变化", "CharacterStateChanges" },
        { "角色状态变更", "CharacterStateChanges" },
        { "角色变化", "CharacterStateChanges" },
        { "冲突进度", "ConflictProgress" },
        { "冲突进展", "ConflictProgress" },
        { "伏笔动作", "ForeshadowingActions" },
        { "伏笔操作", "ForeshadowingActions" },
        { "新增情节", "NewPlotPoints" },
        { "新情节点", "NewPlotPoints" },
        { "新增剧情", "NewPlotPoints" },
        { "地点状态变化", "LocationStateChanges" },
        { "地点状态变更", "LocationStateChanges" },
        { "地点变化", "LocationStateChanges" },
        { "势力状态变化", "FactionStateChanges" },
        { "势力状态变更", "FactionStateChanges" },
        { "势力变化", "FactionStateChanges" },
        { "时间推进", "TimeProgression" },
        { "时间进展", "TimeProgression" },
        { "角色移动", "CharacterMovements" },
        { "角色位移", "CharacterMovements" },
        { "物品流转", "ItemTransfers" },
        { "物品转移", "ItemTransfers" },
        { "道具流转", "ItemTransfers" },
        { "角色ID", "CharacterId" },
        { "角色编号", "CharacterId" },
        { "新等级", "NewLevel" },
        { "新能力", "NewAbilities" },
        { "失去能力", "LostAbilities" },
        { "关系变化", "RelationshipChanges" },
        { "新心理状态", "NewMentalState" },
        { "心理状态", "NewMentalState" },
        { "关键事件", "KeyEvent" },
        { "重要性", "Importance" },
        { "冲突ID", "ConflictId" },
        { "冲突编号", "ConflictId" },
        { "新状态", "NewStatus" },
        { "事件", "Event" },
        { "伏笔ID", "ForeshadowId" },
        { "伏笔编号", "ForeshadowId" },
        { "动作", "Action" },
        { "关键词", "Keywords" },
        { "上下文", "Context" },
        { "涉及角色", "InvolvedCharacters" },
        { "故事线", "Storyline" },
        { "地点ID", "LocationId" },
        { "地点编号", "LocationId" },
        { "势力ID", "FactionId" },
        { "势力编号", "FactionId" },
        { "时间段", "TimePeriod" },
        { "经过时间", "ElapsedTime" },
        { "关键时间事件", "KeyTimeEvent" },
        { "出发地", "FromLocation" },
        { "目的地", "ToLocation" },
        { "物品ID", "ItemId" },
        { "物品编号", "ItemId" },
        { "物品名称", "ItemName" },
        { "原持有者", "FromHolder" },
        { "新持有者", "ToHolder" },
    };
}
