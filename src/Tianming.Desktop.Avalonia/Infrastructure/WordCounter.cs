using System.Text.RegularExpressions;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// CJK 字符 1 个算 1 字；英文按 [A-Za-z]+(?:['-][A-Za-z]+)? 单词正则算 1 词。
/// 标点 / 空格不计。算法与 ChapterContentStore.AccumulateWordCounts 对齐。
/// </summary>
public static class WordCounter
{
    private static readonly Regex EnglishWord = new(@"[A-Za-z]+(?:['-][A-Za-z]+)?", RegexOptions.Compiled);

    public static int Count(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        var cjk = 0;
        foreach (var ch in text)
        {
            if (ch >= '\u4e00' && ch <= '\u9fff') cjk++;
        }
        var en = EnglishWord.Matches(text).Count;
        return cjk + en;
    }
}
