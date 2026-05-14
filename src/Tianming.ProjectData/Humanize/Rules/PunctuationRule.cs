using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Humanize.Rules
{
    public sealed class PunctuationRule : IHumanizeRule
    {
        public string Name => "Punctuation";

        public int Priority => 20;

        public Task<string> ApplyAsync(string input, HumanizeContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(input))
            {
                return Task.FromResult(input);
            }

            var text = input;
            text = Regex.Replace(text, @"(?<=[一-龥]),(?=[一-龥\s])", "，");
            text = Regex.Replace(text, @"(?<=[一-龥])\.(?=[一-龥\s]|$)", "。");
            text = Regex.Replace(text, @"(?<=[一-龥])!(?=[一-龥\s]|$)", "！");
            text = Regex.Replace(text, @"(?<=[一-龥])\?(?=[一-龥\s]|$)", "？");
            return Task.FromResult(text);
        }
    }
}
