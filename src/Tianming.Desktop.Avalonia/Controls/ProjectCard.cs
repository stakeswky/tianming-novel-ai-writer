using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace Tianming.Desktop.Avalonia.Controls;

public class ProjectCard : TemplatedControl
{
    public static readonly StyledProperty<string> ProjectNameProperty =
        AvaloniaProperty.Register<ProjectCard, string>(nameof(ProjectName), string.Empty);
    public static readonly StyledProperty<IImage?> CoverProperty =
        AvaloniaProperty.Register<ProjectCard, IImage?>(nameof(Cover));
    public static readonly StyledProperty<string?> LastOpenedTextProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(LastOpenedText));
    public static readonly StyledProperty<string?> ChapterProgressProperty =
        AvaloniaProperty.Register<ProjectCard, string?>(nameof(ChapterProgress));
    public static readonly StyledProperty<double> ProgressPercentProperty =
        AvaloniaProperty.Register<ProjectCard, double>(nameof(ProgressPercent), 0.0);
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<ProjectCard, ICommand?>(nameof(OpenCommand));

    public string ProjectName { get => GetValue(ProjectNameProperty); set => SetValue(ProjectNameProperty, value); }
    public IImage? Cover { get => GetValue(CoverProperty); set => SetValue(CoverProperty, value); }
    public string? LastOpenedText { get => GetValue(LastOpenedTextProperty); set => SetValue(LastOpenedTextProperty, value); }
    public string? ChapterProgress { get => GetValue(ChapterProgressProperty); set => SetValue(ChapterProgressProperty, value); }
    public double ProgressPercent { get => GetValue(ProgressPercentProperty); set => SetValue(ProgressPercentProperty, value); }
    public ICommand? OpenCommand { get => GetValue(OpenCommandProperty); set => SetValue(OpenCommandProperty, value); }
}
