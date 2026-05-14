using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Humanize.Rules
{
    public sealed class SentenceLengthRule : IHumanizeRule
    {
        private readonly int _longThreshold;

        public SentenceLengthRule(int longThreshold = 40)
        {
            _longThreshold = longThreshold;
        }

        public string Name => "SentenceLength";

        public int Priority => 30;

        public Task<string> ApplyAsync(string input, HumanizeContext context, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(input))
            {
                return Task.FromResult(input);
            }

            var sentences = SplitSentences(input);
            var sb = new StringBuilder();
            var consecutiveLong = 0;

            foreach (var s in sentences)
            {
                if (s.Length >= _longThreshold)
                {
                    consecutiveLong++;
                    if (consecutiveLong == 3)
                    {
                        sb.Append("\n\n");
                        consecutiveLong = 1;
                    }
                }
                else
                {
                    consecutiveLong = 0;
                }

                sb.Append(s);
            }

            return Task.FromResult(sb.ToString());
        }

        private static List<string> SplitSentences(string input)
        {
            var list = new List<string>();
            var sb = new StringBuilder();

            foreach (var ch in input)
            {
                sb.Append(ch);
                if (ch == '。' || ch == '！' || ch == '？' || ch == '\n')
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
            }

            if (sb.Length > 0)
            {
                list.Add(sb.ToString());
            }

            return list;
        }
    }
}
