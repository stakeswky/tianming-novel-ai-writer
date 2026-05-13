using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// 顶部章节 tab 条。Tabs ObservableCollection&lt;ChapterTabItem&gt;；ActiveTab 单选指针。
/// 点击 / 关闭交互由外层 VM 通过 attached command 处理（M4.3 先只渲染，不绑命令——草稿页只挂 1 个 tab）。
/// </summary>
public partial class ChapterTabBar : UserControl
{
    public static readonly StyledProperty<ObservableCollection<ChapterTabItem>?> TabsProperty =
        AvaloniaProperty.Register<ChapterTabBar, ObservableCollection<ChapterTabItem>?>(nameof(Tabs));

    public static readonly StyledProperty<ChapterTabItem?> ActiveTabProperty =
        AvaloniaProperty.Register<ChapterTabBar, ChapterTabItem?>(nameof(ActiveTab));

    public ObservableCollection<ChapterTabItem>? Tabs { get => GetValue(TabsProperty); set => SetValue(TabsProperty, value); }
    public ChapterTabItem? ActiveTab { get => GetValue(ActiveTabProperty); set => SetValue(ActiveTabProperty, value); }

    public ChapterTabBar()
    {
        InitializeComponent();
        SetCurrentValue(TabsProperty, new ObservableCollection<ChapterTabItem>());
    }
}
