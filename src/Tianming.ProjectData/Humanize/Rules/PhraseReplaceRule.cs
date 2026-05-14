using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Humanize.Rules
{
    public sealed class PhraseReplaceRule : IHumanizeRule
    {
        private readonly IReadOnlyDictionary<string, string> _pairs;

        public PhraseReplaceRule(IReadOnlyDictionary<string, string> pairs)
        {
            _pairs = pairs;
        }

        public string Name => "PhraseReplace";

        public int Priority => 10;

        public Task<string> ApplyAsync(string input, HumanizeContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(input) || _pairs.Count == 0)
            {
                return Task.FromResult(input);
            }

            var text = input;
            foreach (var (k, v) in _pairs)
            {
                if (string.IsNullOrEmpty(k))
                {
                    continue;
                }

                text = text.Replace(k, v);
            }

            text = Regex.Replace(text, @"^[，。\s]+", string.Empty);
            text = Regex.Replace(text, @"[ ]{2,}", " ");
            return Task.FromResult(text);
        }
    }
}
