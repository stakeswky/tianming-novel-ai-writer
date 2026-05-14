using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class ConversationPanelViewModel : ObservableObject, IDisposable
{
    private readonly IConversationOrchestrator? _orchestrator;
    private readonly IFileSessionStore? _sessionStore;
    private readonly BulkEmitter? _emitter;
    private readonly IReferenceSuggestionSource? _referenceSuggestionSource;
    private readonly IStagedChangeApprover? _approver;
    private ConversationSession? _currentSession;
    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _referenceSuggestionCts;

    [ObservableProperty] private string _selectedMode = "ask";
    [ObservableProperty] private string _inputDraft = string.Empty;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private SessionListItemVm? _selectedHistoryItem;
    [ObservableProperty] private bool _isHistoryOpen;
    [ObservableProperty] private bool _isReferencePopupOpen;
    [ObservableProperty] private ReferenceItemVm? _selectedReferenceItem;

    public ObservableCollection<SegmentItem> ModeSegments { get; } = new()
    {
        new SegmentItem("ask", "Ask"),
        new SegmentItem("plan", "Plan"),
        new SegmentItem("agent", "Agent"),
    };

    public ObservableCollection<ConversationBubbleVm> SampleBubbles { get; } = new();
    public ObservableCollection<SessionListItemVm> SessionHistory { get; } = new();
    public ObservableCollection<ReferenceItemVm> ReferenceCandidates { get; } = new();

    public ConversationPanelViewModel(bool seedSamples = true, IReferenceSuggestionSource? referenceSuggestionSource = null)
    {
        _referenceSuggestionSource = referenceSuggestionSource;
        if (seedSamples)
            SeedSamples();
    }

    public ConversationPanelViewModel(
        IConversationOrchestrator orchestrator,
        IFileSessionStore sessionStore,
        IDispatcherScheduler scheduler,
        IReferenceSuggestionSource? referenceSuggestionSource = null,
        IStagedChangeApprover? approver = null,
        bool seedSamples = false)
    {
        _orchestrator = orchestrator;
        _sessionStore = sessionStore;
        _emitter = new BulkEmitter(scheduler);
        _referenceSuggestionSource = referenceSuggestionSource;
        _approver = approver;
        _emitter.Start(SampleBubbles);
        if (seedSamples)
            SeedSamples();
    }

    [RelayCommand]
    private void SelectMode(string? key)
    {
        if (!string.IsNullOrWhiteSpace(key))
            SelectedMode = key;
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var input = InputDraft.Trim();
        if (input.Length == 0)
            return;

        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.User,
            Content = input,
            Timestamp = DateTime.Now,
        });

        InputDraft = string.Empty;
        IsStreaming = true;

        if (_orchestrator == null || _sessionStore == null || _emitter == null)
        {
            await SendLocalDemoAsync(input);
            return;
        }

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var mode = ParseChatMode(SelectedMode);
            _currentSession ??= await _orchestrator.StartSessionAsync(mode);
            _currentSession.Mode = mode;

            await foreach (var delta in _orchestrator.SendAsync(_currentSession, input, _cts.Token))
                _emitter.Enqueue(delta);

            await _orchestrator.PersistAsync(_currentSession, _cts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SampleBubbles.Add(new ConversationBubbleVm
            {
                Role = ConversationRole.Assistant,
                Content = $"[错误] {ex.GetType().Name}: {ex.Message}",
                Timestamp = DateTime.Now,
            });
        }
        finally
        {
            IsStreaming = false;
        }
    }

    [RelayCommand]
    private void NewSession()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _emitter?.Stop();
        _currentSession = null;
        SampleBubbles.Clear();
        InputDraft = string.Empty;
        IsStreaming = false;
        _emitter?.Start(SampleBubbles);
    }

    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        if (_sessionStore == null)
            return;

        var sessions = await _sessionStore.ListSessionsAsync();
        SessionHistory.Clear();
        foreach (var session in sessions)
        {
            SessionHistory.Add(new SessionListItemVm
            {
                Id = session.Id,
                Title = string.IsNullOrEmpty(session.Title) ? "（未命名会话）" : session.Title,
                UpdatedAt = session.UpdatedAt,
                MessageCount = session.MessageCount,
            });
        }

        IsHistoryOpen = true;
    }

    [RelayCommand]
    private async Task LoadSessionAsync(string sessionId)
    {
        if (_orchestrator == null || _sessionStore == null)
            return;

        _cts?.Cancel();
        var session = await _sessionStore.LoadSessionAsync(sessionId);
        if (session == null)
            return;

        _currentSession = session;
        SampleBubbles.Clear();
        foreach (var message in session.History)
        {
            SampleBubbles.Add(new ConversationBubbleVm
            {
                Role = message.Role == TM.Services.Framework.AI.SemanticKernel.Conversation.Models.ConversationRole.User
                    ? ConversationRole.User
                    : ConversationRole.Assistant,
                Content = message.Summary,
                Timestamp = message.Timestamp,
            });
        }

        IsHistoryOpen = false;
    }

    [RelayCommand]
    private async Task DeleteSessionAsync(string sessionId)
    {
        if (_sessionStore == null)
            return;

        await _sessionStore.DeleteSessionAsync(sessionId);
        var item = SessionHistory.FirstOrDefault(session => session.Id == sessionId);
        if (item != null)
            SessionHistory.Remove(item);
    }

    [RelayCommand]
    private async Task ApproveStagedAsync(string? stagedId)
    {
        if (_approver == null || string.IsNullOrWhiteSpace(stagedId))
        {
            return;
        }

        await _approver.ApproveAsync(stagedId).ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RejectStagedAsync(string? stagedId)
    {
        if (_approver == null || string.IsNullOrWhiteSpace(stagedId))
        {
            return;
        }

        await _approver.RejectAsync(stagedId).ConfigureAwait(false);
    }

    partial void OnSelectedHistoryItemChanged(SessionListItemVm? value)
    {
        if (value != null)
            _ = LoadSessionAsync(value.Id);
    }

    private static ChatMode ParseChatMode(string mode)
        => mode.ToLowerInvariant() switch
        {
            "plan" => ChatMode.Plan,
            "agent" => ChatMode.Agent,
            _ => ChatMode.Ask,
        };

    private async Task SendLocalDemoAsync(string input)
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            ThinkingBlock = BuildThinkingBlock(input),
            Content = BuildLocalDemoResponse(input),
            Timestamp = DateTime.Now,
        });

        await Task.Yield();
        IsStreaming = false;
    }

    partial void OnInputDraftChanged(string value)
    {
        var index = value.LastIndexOf('@');
        if (index < 0 || index == value.Length - 1)
        {
            ClearReferenceSuggestions();
            return;
        }

        var query = value[(index + 1)..];
        if (string.IsNullOrWhiteSpace(query))
        {
            ClearReferenceSuggestions();
            return;
        }

        _referenceSuggestionCts?.Cancel();
        _referenceSuggestionCts?.Dispose();
        _referenceSuggestionCts = new CancellationTokenSource();
        _ = PopulateReferenceCandidatesAsync(query, _referenceSuggestionCts.Token);
    }

    private async Task PopulateReferenceCandidatesAsync(string query, CancellationToken ct)
    {
        var candidates = _referenceSuggestionSource == null
            ? GetFallbackReferenceCandidates(query)
            : await _referenceSuggestionSource.SuggestAsync(query, ct);

        if (ct.IsCancellationRequested)
            return;

        ReferenceCandidates.Clear();
        foreach (var candidate in candidates.Take(10))
            ReferenceCandidates.Add(candidate);

        IsReferencePopupOpen = ReferenceCandidates.Count > 0;
    }

    private static IReadOnlyList<ReferenceItemVm> GetFallbackReferenceCandidates(string query)
    {
        var samples = new[]
        {
            new ReferenceItemVm { Id = "ch-001", Name = "第 1 章 风起青萍", Category = "Chapter" },
            new ReferenceItemVm { Id = "char-zhuge", Name = "诸葛清", Category = "Character" },
            new ReferenceItemVm { Id = "world-jiuzhou", Name = "九州大陆", Category = "World" },
        };

        return samples
            .Where(sample => sample.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void ClearReferenceSuggestions()
    {
        _referenceSuggestionCts?.Cancel();
        _referenceSuggestionCts?.Dispose();
        _referenceSuggestionCts = null;
        ReferenceCandidates.Clear();
        IsReferencePopupOpen = false;
    }

    [RelayCommand]
    private void SelectReference(ReferenceItemVm? item)
    {
        if (item == null)
            return;

        var index = InputDraft.LastIndexOf('@');
        if (index < 0)
            return;

        InputDraft = InputDraft[..index] + $"@{item.Name} ";
        IsReferencePopupOpen = false;
    }

    partial void OnSelectedReferenceItemChanged(ReferenceItemVm? value)
    {
        if (value != null)
            SelectReferenceCommand.Execute(value);
    }

    private string BuildThinkingBlock(string input)
    {
        var mode = FormatMode(SelectedMode);
        return $"{mode} 模式本地预览\n输入长度：{input.Length}\n下一步：接入 ConversationOrchestrator 流式输出";
    }

    private string BuildLocalDemoResponse(string input)
    {
        return FormatMode(SelectedMode) switch
        {
            "Plan" => $"Plan 预览：已收到「{input}」。\n1. 明确章节目标\n2. 拆解冲突与转折\n3. 输出可执行写作步骤",
            "Agent" => $"Agent 预览：已收到「{input}」。工具调用通道已预留，后续接 LookupData / ReadChapter / SearchReferences。",
            _ => $"Ask 预览：已收到「{input}」。右栏已可交互，后续接真实流式 AI 回复。",
        };
    }

    private static string FormatMode(string mode)
        => mode.Equals("plan", StringComparison.OrdinalIgnoreCase)
            ? "Plan"
            : mode.Equals("agent", StringComparison.OrdinalIgnoreCase)
                ? "Agent"
                : "Ask";

    private void SeedSamples()
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.User,
            Content = "帮我写第 32 章后半段，主角与沈砚在雨夜的对峙，需要带出二人未解的旧账。",
            Timestamp = DateTime.Now.AddMinutes(-12),
        });
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            ThinkingBlock = "用户要求：主角 vs 沈砚 / 雨夜 / 旧账。\n需调取角色档案：沈砚的动机线，主角失忆背景。\n确认情绪基调：克制中带火药味。",
            Content = "好的。第 32 章后半段提纲已生成：\n1. 雨幕拉开两人距离，沈砚先开口。\n2. 主角拒绝回应，转身欲走。\n3. 沈砚抛出关键线索（旧友失踪日期），主角僵住。\n4. 两人对峙在屋檐下，台词压低但句句带刺。\n\n要我按这个 beat 直接续写正文，还是先扩写每段大意？",
            Timestamp = DateTime.Now.AddMinutes(-11),
        });
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _referenceSuggestionCts?.Cancel();
        _referenceSuggestionCts?.Dispose();
        _emitter?.Stop();
    }
}

public sealed class BulkEmitter
{
    private readonly IDispatcherScheduler _scheduler;
    private readonly Queue<ChatStreamDelta> _pending = new();
    private readonly object _lock = new();
    private ObservableCollection<ConversationBubbleVm>? _bubbles;
    private IDisposable? _handle;

    public BulkEmitter()
        : this(new ImmediateDispatcherScheduler())
    {
    }

    public BulkEmitter(IDispatcherScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    public void Start(ObservableCollection<ConversationBubbleVm> bubbles)
    {
        _bubbles = bubbles;
        _handle?.Dispose();
        _handle = _scheduler.ScheduleRecurring(TimeSpan.FromMilliseconds(16), Flush);
    }

    public void Stop()
    {
        _handle?.Dispose();
        _handle = null;
        lock (_lock)
            _pending.Clear();
    }

    public void Enqueue(ChatStreamDelta delta)
    {
        lock (_lock)
            _pending.Enqueue(delta);
    }

    public void Apply(ObservableCollection<ConversationBubbleVm> bubbles, ChatStreamDelta delta)
    {
        if (bubbles == null)
            throw new ArgumentNullException(nameof(bubbles));

        var assistant = EnsureAssistantBubble(bubbles);
        switch (delta)
        {
            case ThinkingDelta thinking:
                assistant.ThinkingBlock = (assistant.ThinkingBlock ?? string.Empty) + thinking.Text;
                break;
            case AnswerDelta answer:
                assistant.Content += answer.Text;
                break;
            case ToolCallDelta tool:
                assistant.Content += $"\n[tool:{tool.ToolName}] {tool.ArgumentsJson}";
                break;
            case ToolResultDelta result:
                assistant.Content += $"\n[result:{result.ToolCallId}] {result.ResultText}";
                break;
            case PlanStepDelta step:
                assistant.Content += $"\n{step.Step.Index}. {step.Step.Title}";
                break;
        }
    }

    private void Flush()
    {
        if (_bubbles == null)
            return;

        ChatStreamDelta[] batch;
        lock (_lock)
        {
            if (_pending.Count == 0)
                return;

            batch = _pending.ToArray();
            _pending.Clear();
        }

        foreach (var delta in batch)
            Apply(_bubbles, delta);
    }

    private static ConversationBubbleVm EnsureAssistantBubble(ObservableCollection<ConversationBubbleVm> bubbles)
    {
        if (bubbles.Count > 0 && bubbles[^1].Role == ConversationRole.Assistant)
            return bubbles[^1];

        var assistant = new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
        };
        bubbles.Add(assistant);
        return assistant;
    }

    private sealed class ImmediateDispatcherScheduler : IDispatcherScheduler
    {
        public IDisposable ScheduleRecurring(TimeSpan interval, Action callback) => new Disposable();

        public void Post(Action callback) => callback();

        private sealed class Disposable : IDisposable
        {
            public void Dispose()
            {
            }
        }
    }
}
