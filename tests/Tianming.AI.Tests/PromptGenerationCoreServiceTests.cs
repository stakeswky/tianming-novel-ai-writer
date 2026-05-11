using TM.Framework.Common.Helpers.AI;
using Xunit;

namespace Tianming.AI.Tests;

public class PromptGenerationCoreServiceTests
{
    [Fact]
    public async Task GenerateModulePromptAsync_rejects_context_without_module_identity()
    {
        var generator = new CapturingGenerator(new PromptGenerationAiResult(true, "{}"));
        var service = new PromptGenerationCoreService(generator);

        var result = await service.GenerateModulePromptAsync(new PromptGenerationContext());

        Assert.False(result.Success);
        Assert.Equal("缺少模块标识或名称", result.ErrorMessage);
        Assert.Empty(generator.Requests);
    }

    [Fact]
    public async Task GenerateModulePromptAsync_parses_json_object_from_ai_response()
    {
        var generator = new CapturingGenerator(new PromptGenerationAiResult(true, """
        下面是结果：
        {
          "content": "系统提示词正文",
          "name": "模板名",
          "description": "模板描述",
          "tags": ["设计", "生成"]
        }
        """));
        var service = new PromptGenerationCoreService(generator);

        var result = await service.GenerateModulePromptAsync(new PromptGenerationContext
        {
            ModuleKey = "chapter",
            ModuleDisplayName = "章节规划",
            ExtraRequirement = "更热血",
            OutputFieldNames = ["标题", "正文"],
            InputVariableNames = ["章节标题"]
        });

        Assert.True(result.Success);
        Assert.Equal("系统提示词正文", result.Content);
        Assert.Equal("模板名", result.Name);
        Assert.Equal("模板描述", result.Description);
        Assert.Equal(["设计", "生成"], result.Tags);
        Assert.Single(generator.Requests);
        Assert.Contains("章节规划", generator.Requests[0]);
        Assert.Contains("{章节标题}", generator.Requests[0]);
        Assert.Contains("标题", generator.Requests[0]);
    }

    [Fact]
    public async Task GenerateModulePromptAsync_splits_string_tags_and_uses_extra_requirement_as_description_fallback()
    {
        var generator = new CapturingGenerator(new PromptGenerationAiResult(true, """
        { "content": "正文", "tags": "A，B,C" }
        """));
        var service = new PromptGenerationCoreService(generator);

        var result = await service.GenerateModulePromptAsync(new PromptGenerationContext
        {
            ModuleKey = "outline",
            ExtraRequirement = "需要短句"
        });

        Assert.True(result.Success);
        Assert.Equal("正文", result.Content);
        Assert.Equal("需要短句", result.Description);
        Assert.Equal(["A", "B", "C"], result.Tags);
    }

    [Fact]
    public async Task GenerateModulePromptAsync_falls_back_to_plain_text_when_response_is_not_valid_json()
    {
        var generator = new CapturingGenerator(new PromptGenerationAiResult(true, "直接输出的系统提示词"));
        var service = new PromptGenerationCoreService(generator);

        var result = await service.GenerateModulePromptAsync(new PromptGenerationContext
        {
            ModuleDisplayName = "角色规则",
            ExtraRequirement = "保持克制"
        });

        Assert.True(result.Success);
        Assert.Equal("直接输出的系统提示词", result.Content);
        Assert.Equal("保持克制", result.Description);
    }

    [Fact]
    public async Task GenerateModulePromptAsync_maps_ai_failure_to_result_error()
    {
        var generator = new CapturingGenerator(new PromptGenerationAiResult(false, "", "限流"));
        var service = new PromptGenerationCoreService(generator);

        var result = await service.GenerateModulePromptAsync(new PromptGenerationContext
        {
            ModuleKey = "plot"
        });

        Assert.False(result.Success);
        Assert.Equal("限流", result.ErrorMessage);
    }

    private sealed class CapturingGenerator : IPromptTextGenerator
    {
        private readonly PromptGenerationAiResult _result;

        public List<string> Requests { get; } = new();

        public CapturingGenerator(PromptGenerationAiResult result)
        {
            _result = result;
        }

        public Task<PromptGenerationAiResult> GenerateAsync(string prompt)
        {
            Requests.Add(prompt);
            return Task.FromResult(_result);
        }
    }
}
