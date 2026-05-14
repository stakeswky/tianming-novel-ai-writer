using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Humanize
{
    /// <summary>
    /// 单条去 AI 味规则：纯文本输入 → 处理后文本输出。
    /// 多条规则在 HumanizePipeline 中按 Priority 升序串行执行。
    /// </summary>
    public interface IHumanizeRule
    {
        string Name { get; }

        int Priority { get; }

        Task<string> ApplyAsync(string input, HumanizeContext context, CancellationToken ct = default);
    }
}
