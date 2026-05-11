using System;
using System.Text;
using System.Text.RegularExpressions;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;

public sealed class TagBasedThinkingStrategy
{
    private static readonly string[] ThinkingOpenTags = { "<think>", "<analysis>" };
    private static readonly Regex NoiseLinePattern = new(
        @"^(?:Thought\s+for\s+[\d\.]+\s*s|Thinking\.{2,})\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const string AnswerOpenTag = "<answer>";
    private const string AnswerCloseTag = "</answer>";

    private readonly StringBuilder _buffer = new();
    private TagParseState _state = TagParseState.Content;
    private string? _activeCloseTag;

    private enum TagParseState
    {
        Content,
        InThinking,
        BetweenTags,
        InAnswer,
        Done
    }

    public ThinkingRouteResult Extract(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return default;

        _buffer.Append(text);
        return ParseBuffer();
    }

    public ThinkingRouteResult Flush()
    {
        if (_buffer.Length == 0)
            return default;

        var remaining = _buffer.ToString();
        _buffer.Clear();

        return _state switch
        {
            TagParseState.InThinking => new ThinkingRouteResult { ThinkingContent = remaining },
            TagParseState.InAnswer or TagParseState.Done => new ThinkingRouteResult { AnswerContent = remaining },
            _ => new ThinkingRouteResult { AnswerContent = string.IsNullOrWhiteSpace(remaining) ? null : remaining }
        };
    }

    private ThinkingRouteResult ParseBuffer()
    {
        StringBuilder? thinkingOut = null;
        StringBuilder? answerOut = null;
        var text = _buffer.ToString();
        var consumed = 0;

        while (consumed < text.Length)
        {
            switch (_state)
            {
                case TagParseState.Content:
                {
                    var (tagPos, tag) = FindFirst(text, consumed, ThinkingOpenTags);
                    var answerPos = IndexOfCI(text, AnswerOpenTag, consumed);

                    if (tagPos >= 0 && (answerPos < 0 || tagPos <= answerPos))
                    {
                        AppendAnswerBeforeTag(text, consumed, tagPos, ref answerOut);
                        _activeCloseTag = GetMatchingCloseTag(tag);
                        consumed = tagPos + tag.Length;
                        _state = TagParseState.InThinking;
                    }
                    else if (answerPos >= 0)
                    {
                        AppendAnswerBeforeTag(text, consumed, answerPos, ref answerOut);
                        consumed = answerPos + AnswerOpenTag.Length;
                        _state = TagParseState.InAnswer;
                    }
                    else
                    {
                        var tail = text[consumed..];
                        var safeCount = FindSafePrefixLength(tail, ThinkingOpenTags, AnswerOpenTag);
                        if (safeCount < tail.Length)
                        {
                            AppendAnswerBeforeTag(tail, 0, safeCount, ref answerOut);
                            consumed += safeCount;
                            goto done;
                        }

                        var output = StripHighConfidenceNoise(tail);
                        if (!string.IsNullOrEmpty(output))
                            (answerOut ??= new()).Append(output);
                        consumed = text.Length;
                    }
                    break;
                }

                case TagParseState.InThinking:
                {
                    var closeTag = _activeCloseTag ?? "</think>";
                    var end = IndexOfCI(text, closeTag, consumed);
                    if (end >= 0)
                    {
                        if (end > consumed)
                            (thinkingOut ??= new()).Append(text, consumed, end - consumed);
                        consumed = end + closeTag.Length;
                        _state = TagParseState.BetweenTags;
                    }
                    else
                    {
                        var tail = text[consumed..];
                        var safeCount = FindSafePrefixLength(tail, closeTag);
                        if (safeCount < tail.Length)
                        {
                            if (safeCount > 0)
                                (thinkingOut ??= new()).Append(tail, 0, safeCount);
                            consumed += safeCount;
                            goto done;
                        }

                        (thinkingOut ??= new()).Append(tail);
                        consumed = text.Length;
                    }
                    break;
                }

                case TagParseState.BetweenTags:
                {
                    var answerPos = IndexOfCI(text, AnswerOpenTag, consumed);
                    var (thinkPos, thinkTag) = FindFirst(text, consumed, ThinkingOpenTags);

                    if (answerPos >= 0 && (thinkPos < 0 || answerPos <= thinkPos))
                    {
                        consumed = answerPos + AnswerOpenTag.Length;
                        _state = TagParseState.InAnswer;
                    }
                    else if (thinkPos >= 0)
                    {
                        _activeCloseTag = GetMatchingCloseTag(thinkTag);
                        consumed = thinkPos + thinkTag.Length;
                        _state = TagParseState.InThinking;
                    }
                    else
                    {
                        var tail = text[consumed..];
                        var safeCount = FindSafePrefixLength(tail, ThinkingOpenTags, AnswerOpenTag);
                        if (safeCount < tail.Length)
                        {
                            var safe = tail[..safeCount].TrimStart('\r', '\n');
                            if (!string.IsNullOrEmpty(safe))
                                (answerOut ??= new()).Append(safe);
                            consumed += safeCount;
                            goto done;
                        }

                        var output = tail.TrimStart('\r', '\n');
                        if (!string.IsNullOrEmpty(output))
                            (answerOut ??= new()).Append(output);
                        consumed = text.Length;
                    }
                    break;
                }

                case TagParseState.InAnswer:
                {
                    var end = IndexOfCI(text, AnswerCloseTag, consumed);
                    if (end >= 0)
                    {
                        if (end > consumed)
                            (answerOut ??= new()).Append(text, consumed, end - consumed);
                        consumed = end + AnswerCloseTag.Length;
                        _state = TagParseState.Done;
                    }
                    else
                    {
                        var tail = text[consumed..];
                        var safeCount = FindSafePrefixLength(tail, AnswerCloseTag);
                        if (safeCount < tail.Length)
                        {
                            if (safeCount > 0)
                                (answerOut ??= new()).Append(tail, 0, safeCount);
                            consumed += safeCount;
                            goto done;
                        }

                        (answerOut ??= new()).Append(tail);
                        consumed = text.Length;
                    }
                    break;
                }

                case TagParseState.Done:
                default:
                    if (consumed < text.Length)
                        (answerOut ??= new()).Append(text, consumed, text.Length - consumed);
                    consumed = text.Length;
                    goto done;
            }
        }

    done:
        if (consumed > 0 && consumed <= _buffer.Length)
            _buffer.Remove(0, consumed);

        return new ThinkingRouteResult
        {
            ThinkingContent = thinkingOut?.ToString(),
            AnswerContent = answerOut?.ToString()
        };
    }

    private static void AppendAnswerBeforeTag(string source, int start, int end, ref StringBuilder? answerOut)
    {
        if (end <= start)
            return;

        var before = StripHighConfidenceNoise(source.Substring(start, end - start));
        if (!string.IsNullOrEmpty(before))
            (answerOut ??= new()).Append(before);
    }

    private static int IndexOfCI(string source, string value, int startIndex)
    {
        return startIndex >= source.Length
            ? -1
            : source.IndexOf(value, startIndex, StringComparison.OrdinalIgnoreCase);
    }

    private static (int Index, string Tag) FindFirst(string source, int startIndex, string[] tags)
    {
        var bestIndex = -1;
        var bestTag = string.Empty;
        foreach (var tag in tags)
        {
            var index = IndexOfCI(source, tag, startIndex);
            if (index >= 0 && (bestIndex < 0 || index < bestIndex))
            {
                bestIndex = index;
                bestTag = tag;
            }
        }

        return (bestIndex, bestTag);
    }

    private static string GetMatchingCloseTag(string openTag)
    {
        return openTag.Equals("<think>", StringComparison.OrdinalIgnoreCase) ? "</think>" : "</analysis>";
    }

    private static int FindSafePrefixLength(string tail, string[] tags, string singleTag)
    {
        var lastLt = tail.LastIndexOf('<');
        if (lastLt < 0)
            return tail.Length;

        var suffix = tail[lastLt..];
        foreach (var tag in tags)
        {
            if (tag.StartsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return lastLt;
        }

        return singleTag.StartsWith(suffix, StringComparison.OrdinalIgnoreCase) ? lastLt : tail.Length;
    }

    private static int FindSafePrefixLength(string tail, string tag)
    {
        var lastLt = tail.LastIndexOf('<');
        if (lastLt < 0)
            return tail.Length;

        var suffix = tail[lastLt..];
        return tag.StartsWith(suffix, StringComparison.OrdinalIgnoreCase) ? lastLt : tail.Length;
    }

    private static string StripHighConfidenceNoise(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var trimmed = text.TrimStart();
        if (trimmed.Length == 0)
            return text;

        var eol = trimmed.IndexOf('\n');
        var firstLine = eol >= 0 ? trimmed[..eol] : trimmed;
        if (!NoiseLinePattern.IsMatch(firstLine))
            return text;

        if (eol < 0)
            return string.Empty;

        var rest = trimmed[(eol + 1)..];
        return string.IsNullOrWhiteSpace(rest) ? string.Empty : rest;
    }
}
