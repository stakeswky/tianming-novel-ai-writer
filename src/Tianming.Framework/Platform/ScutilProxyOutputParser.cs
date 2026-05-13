using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace TM.Framework.Platform;

/// <summary>
/// 解析 <c>/usr/sbin/scutil --proxy</c> 命令的纯文本输出（pseudo-plist 字典格式），
/// 提取 HTTP / HTTPS 代理与 bypass 例外列表，返回 <see cref="ProxyPolicy"/>。
/// 任何无法识别 / 空输入 / 仅启用但缺字段都退化为 <see cref="ProxyPolicy.Direct"/>。
/// </summary>
public static class ScutilProxyOutputParser
{
    private static readonly Regex KeyValue = new(
        @"^\s*([A-Za-z]+)\s*:\s*(.+?)\s*$",
        RegexOptions.Compiled);

    private static readonly Regex ArrayItem = new(
        @"^\s*\d+\s*:\s*(.+?)\s*$",
        RegexOptions.Compiled);

    public static ProxyPolicy Parse(string scutilOutput)
    {
        if (string.IsNullOrWhiteSpace(scutilOutput))
            return ProxyPolicy.Direct;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var exceptions = new List<string>();
        var inExceptions = false;

        foreach (var raw in scutilOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.TrimEnd('\r');

            if (line.Contains("ExceptionsList", StringComparison.Ordinal)
                && line.Contains("<array>", StringComparison.Ordinal))
            {
                // 单行内联形式：`ExceptionsList : <array> { 0 : localhost 1 : *.local }`
                if (line.Contains('{', StringComparison.Ordinal)
                    && line.Contains('}', StringComparison.Ordinal))
                {
                    var open = line.IndexOf('{', StringComparison.Ordinal);
                    var close = line.LastIndexOf('}');
                    if (open >= 0 && close > open)
                    {
                        var inner = line.Substring(open + 1, close - open - 1);
                        foreach (var part in SplitInlineArrayItems(inner))
                            exceptions.Add(part);
                    }
                    inExceptions = false;
                    continue;
                }
                inExceptions = true;
                continue;
            }

            if (inExceptions)
            {
                if (line.Contains('}', StringComparison.Ordinal))
                {
                    inExceptions = false;
                    continue;
                }
                var am = ArrayItem.Match(line);
                if (am.Success)
                    exceptions.Add(am.Groups[1].Value);
                continue;
            }

            var m = KeyValue.Match(line);
            if (!m.Success) continue;

            // 跳过结构性标记行（"<array>"、"<dictionary>"）
            var value = m.Groups[2].Value;
            if (value.StartsWith('<') && value.EndsWith('>'))
                continue;

            values[m.Groups[1].Value] = value;
        }

        var http = BuildUri(values, "HTTPEnable", "HTTPProxy", "HTTPPort");
        var https = BuildUri(values, "HTTPSEnable", "HTTPSProxy", "HTTPSPort");

        if (http is null && https is null && exceptions.Count == 0)
            return ProxyPolicy.Direct;

        return new ProxyPolicy(http, https, exceptions);
    }

    private static IEnumerable<string> SplitInlineArrayItems(string inner)
    {
        // 形如 " 0 : localhost 1 : *.local " — 按 "数字 :" 边界切分，取每段之间的非空 token。
        // scutil 实际很少用单行 inline 格式（只在测试 fixture 里出现），所以采用宽松匹配。
        var matches = Regex.Matches(inner, @"\d+\s*:\s*(\S+)");
        foreach (Match m in matches)
        {
            var v = m.Groups[1].Value.Trim();
            if (v.Length > 0) yield return v;
        }
    }

    private static Uri? BuildUri(
        IReadOnlyDictionary<string, string> values,
        string enableKey,
        string hostKey,
        string portKey)
    {
        if (!values.TryGetValue(enableKey, out var enable) || enable != "1")
            return null;
        if (!values.TryGetValue(hostKey, out var host) || string.IsNullOrWhiteSpace(host))
            return null;
        if (!values.TryGetValue(portKey, out var port) || !int.TryParse(port, out var p))
            return null;
        return new Uri($"http://{host}:{p}");
    }
}
