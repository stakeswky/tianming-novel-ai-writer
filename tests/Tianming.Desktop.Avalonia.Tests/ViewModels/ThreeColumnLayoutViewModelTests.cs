using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using Tianming.Desktop.Avalonia.ViewModels.Book;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels;

public class ThreeColumnLayoutViewModelTests
{
    [Fact]
    public async Task Navigate_to_book_pipeline_replaces_previous_center_page()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var layout = sp.GetRequiredService<ThreeColumnLayoutViewModel>();
        var nav = sp.GetRequiredService<INavigationService>();

        await nav.NavigateAsync(PageKeys.AIUsage);
        Assert.IsType<UsageStatisticsViewModel>(layout.Center);

        await nav.NavigateAsync(PageKeys.BookPipeline);

        Assert.IsType<BookPipelineViewModel>(layout.Center);
    }

    [Fact]
    public async Task Navigate_to_book_pipeline_uses_user_visible_breadcrumb_label()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var chrome = sp.GetRequiredService<AppChromeViewModel>();
        var nav = sp.GetRequiredService<INavigationService>();

        await nav.NavigateAsync(PageKeys.BookPipeline);

        Assert.Equal("一键成书", chrome.Segments[^1].Label);
    }
}
