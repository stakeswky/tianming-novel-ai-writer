using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;

namespace Tianming.Desktop.Avalonia.ViewModels;

/// <summary>单条活动 feed 条目（用于 activityFeed ListBox）。</summary>
public sealed record ActivityItemVm(string Title, string Subtitle, string Timestamp, string Kind);

/// <summary>
/// M3 仪表盘 ViewModel —— 全部 hardcoded 示例数据。
/// M4 起接 FileProjectManager 真实数据。
/// </summary>
public partial class DashboardViewModel : ObservableObject
{
    // ============ Stats Cards ============
    [ObservableProperty] private string _projectName       = "山河长安";
    [ObservableProperty] private string _projectSubtitle   = "32 / 60 章 · 上次更新 2026-05-11 22:14";

    [ObservableProperty] private string _totalWordsValue       = "186,420";
    [ObservableProperty] private string _totalWordsCaption     = "+ 3,180 / 今天";
    [ObservableProperty] private string _chapterCountValue     = "32";
    [ObservableProperty] private string _chapterCountCaption   = "60 章总规划";
    [ObservableProperty] private string _characterCountValue   = "18";
    [ObservableProperty] private string _characterCountCaption = "主线 6 / 配角 12";
    [ObservableProperty] private string _debtCountValue        = "3";
    [ObservableProperty] private string _debtCountCaption      = "需要在 5 章内回收";
    [ObservableProperty] private string _chapterProgressValue   = "92%";
    [ObservableProperty] private string _chapterProgressCaption = "当前章节 · 第 32 章";

    // ============ 章节进度饼图 ============
    public ISeries[] ChapterProgressSeries { get; }

    // ============ 学习曲线（过去 30 天每日字数） ============
    public ISeries[] LearningCurveSeries { get; }
    public Axis[]    LearningCurveXAxes  { get; }
    public Axis[]    LearningCurveYAxes  { get; }

    // ============ Activity Feed ============
    public ObservableCollection<ActivityItemVm> ActivityFeed { get; } = new();

    public DashboardViewModel()
    {
        // 章节进度饼图：92% 完成 + 8% 未完成
        ChapterProgressSeries = new ISeries[]
        {
            new PieSeries<double>
            {
                Values            = new double[] { 92 },
                Name              = "已完成",
                Fill              = new SolidColorPaint(new SKColor(16, 185, 129)),  // StatusSuccess
                InnerRadius       = 56,
                DataLabelsSize    = 0,
            },
            new PieSeries<double>
            {
                Values            = new double[] { 8 },
                Name              = "剩余",
                Fill              = new SolidColorPaint(new SKColor(226, 232, 240)), // SurfaceMuted
                InnerRadius       = 56,
                DataLabelsSize    = 0,
            },
        };

        // 学习曲线：过去 30 天每日字数（mock 折线，1500 ~ 3500 浮动）
        var rng    = new Random(42);
        var values = new int[30];
        var baseV  = 2300;
        for (int i = 0; i < 30; i++)
        {
            baseV += rng.Next(-450, 600);
            if (baseV < 1500) baseV = 1500;
            if (baseV > 3800) baseV = 3800;
            values[i] = baseV;
        }
        LearningCurveSeries = new ISeries[]
        {
            new LineSeries<int>
            {
                Values             = values,
                Name               = "每日字数",
                Fill               = new SolidColorPaint(new SKColor(6, 182, 212, 32)),    // AccentBase 透明填充
                Stroke             = new SolidColorPaint(new SKColor(6, 182, 212)) { StrokeThickness = 2 },
                GeometryFill       = new SolidColorPaint(new SKColor(6, 182, 212)),
                GeometryStroke     = new SolidColorPaint(SKColors.White) { StrokeThickness = 1 },
                GeometrySize       = 6,
                LineSmoothness     = 0.4,
            },
        };

        var dayLabels = new string[30];
        var today     = DateTime.Today;
        for (int i = 0; i < 30; i++)
            dayLabels[i] = today.AddDays(i - 29).ToString("MM-dd");

        LearningCurveXAxes = new[]
        {
            new Axis
            {
                Labels           = dayLabels,
                LabelsRotation   = 0,
                MinStep          = 5,
                TextSize         = 10,
                LabelsPaint      = new SolidColorPaint(new SKColor(148, 163, 184)),
                SeparatorsPaint  = null,
            },
        };
        LearningCurveYAxes = new[]
        {
            new Axis
            {
                MinLimit        = 0,
                TextSize        = 10,
                LabelsPaint     = new SolidColorPaint(new SKColor(148, 163, 184)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(241, 245, 249)) { StrokeThickness = 1 },
            },
        };

        SeedActivityFeed();
    }

    private void SeedActivityFeed()
    {
        var feed = new List<ActivityItemVm>
        {
            new("生成草稿 · 第 32 章 后半段",     "AI 生成 1,840 字，已合并到草稿",  "10 分钟前",      "draft"),
            new("校验通过",                        "三段连续 / 复读 / 标点 全部 OK",   "32 分钟前",      "validation"),
            new("更新角色档案 · 沈砚",             "新增动机：寻回旧友失踪线索",        "1 小时前",       "character"),
            new("生成大纲 · 第 33 章",             "AI 生成 6 个 beats，已写入大纲",   "今天 14:02",     "outline"),
            new("绑定 OpenAI 密钥",                 "Keychain 已存证",                    "今天 09:45",     "settings"),
            new("打开项目",                          "山河长安",                            "今天 09:32",     "session"),
            new("校验失败 · 第 31 章",             "三段连续 2 处 / 复读 1 处",         "昨天 22:11",     "validation-fail"),
            new("写作 1,420 字",                    "草稿 · 第 31 章 段落 12-18",         "昨天 21:35",     "draft"),
            new("生成大纲 · 第 31 章",             "AI 生成 5 个 beats",                 "昨天 19:50",     "outline"),
            new("新建角色 · 林若初",               "配角 / 出场章节 28",                 "昨天 18:24",     "character"),
        };
        foreach (var item in feed)
            ActivityFeed.Add(item);
    }
}
