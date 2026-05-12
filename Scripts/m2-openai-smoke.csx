#r "../src/Tianming.AI/bin/Debug/net8.0/Tianming.AI.dll"
#r "../src/Tianming.AI/bin/Debug/net8.0/Tianming.ProjectData.dll"

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;

// 读取配置：$HOME/.tianming/smoke.json
//   { "BaseUrl": "https://api.deepseek.com", "ApiKey": "sk-...", "Model": "deepseek-chat" }
var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var cfgPath = Path.Combine(home, ".tianming", "smoke.json");
if (!File.Exists(cfgPath))
{
    Console.Error.WriteLine($"请先创建 {cfgPath}，内容示例：");
    Console.Error.WriteLine("{ \"BaseUrl\": \"https://api.deepseek.com\", \"ApiKey\": \"sk-xxx\", \"Model\": \"deepseek-chat\" }");
    Environment.Exit(1);
}

using var cfgStream = File.OpenRead(cfgPath);
var cfg = JsonSerializer.Deserialize<JsonElement>(cfgStream);
var baseUrl = cfg.GetProperty("BaseUrl").GetString()!;
var apiKey  = cfg.GetProperty("ApiKey").GetString()!;
var model   = cfg.GetProperty("Model").GetString()!;

using var http = new HttpClient();
var client = new OpenAICompatibleChatClient(http);

var request = new OpenAICompatibleChatRequest
{
    BaseUrl = baseUrl,
    ApiKey = apiKey,
    Model = model,
    Temperature = 0.3,
    MaxTokens = 256,
    Messages =
    {
        new OpenAICompatibleChatMessage("system", "你是天命 macOS 迁移项目的冒烟测试助手，请用一句话回复。"),
        new OpenAICompatibleChatMessage("user", "今天是 2026 年 5 月 12 日，请只回复：收到。")
    }
};

Console.WriteLine("=== 非流式 ===");
var result = await client.CompleteAsync(request, CancellationToken.None);
Console.WriteLine($"Success={result.Success} Status={result.StatusCode}");
Console.WriteLine($"Content: {result.Content}");
Console.WriteLine($"Tokens: prompt={result.PromptTokens} completion={result.CompletionTokens} total={result.TotalTokens}");
if (!string.IsNullOrEmpty(result.ErrorMessage))
    Console.WriteLine($"Error: {result.ErrorMessage}");

Console.WriteLine("\n=== 流式 ===");
var chunks = 0;
await foreach (var chunk in client.StreamAsync(request, CancellationToken.None))
{
    Console.Write(chunk.Content);
    chunks++;
    if (chunk.FinishReason is not null)
        Console.WriteLine($"\n[finish_reason={chunk.FinishReason}]");
}
Console.WriteLine($"\n收到 {chunks} 个流式分片。");
