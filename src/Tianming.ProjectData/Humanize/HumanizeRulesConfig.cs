using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Humanize
{
    public sealed class HumanizeRulesConfig
    {
        [JsonPropertyName("PhraseReplacements")]
        public Dictionary<string, string> PhraseReplacements { get; set; } = DefaultPhrases();

        [JsonPropertyName("EnablePunctuation")]
        public bool EnablePunctuation { get; set; } = true;

        [JsonPropertyName("SentenceLongThreshold")]
        public int SentenceLongThreshold { get; set; } = 40;

        private static Dictionary<string, string> DefaultPhrases() => new()
        {
            ["总而言之"] = string.Empty,
            ["综上所述"] = string.Empty,
            ["不可否认"] = string.Empty,
            ["毋庸置疑"] = string.Empty,
            ["在我看来"] = string.Empty,
            ["让我们"] = string.Empty,
        };
    }
}
