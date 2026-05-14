using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Humanize
{
    public sealed class HumanizePipeline
    {
        private readonly IReadOnlyList<IHumanizeRule> _rules;

        public HumanizePipeline(IEnumerable<IHumanizeRule> rules)
        {
            _rules = rules.OrderBy(r => r.Priority).ToList();
        }

        public IReadOnlyList<string> RuleNames => _rules.Select(r => r.Name).ToList();

        public async Task<string> RunAsync(string input, HumanizeContext context, CancellationToken ct = default)
        {
            var text = input;
            foreach (var rule in _rules)
            {
                ct.ThrowIfCancellationRequested();
                text = await rule.ApplyAsync(text, context, ct).ConfigureAwait(false);
            }

            return text;
        }
    }
}
