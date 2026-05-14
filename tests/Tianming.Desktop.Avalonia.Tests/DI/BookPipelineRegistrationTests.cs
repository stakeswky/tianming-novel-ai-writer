using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.BookPipeline;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Book;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class BookPipelineRegistrationTests
{
    [Fact]
    public void Build_registers_book_pipeline_page_and_services()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var reg = sp.GetRequiredService<PageRegistry>();

        Assert.Contains(PageKeys.BookPipeline, reg.Keys);
        Assert.NotNull(sp.GetRequiredService<BookPipelineViewModel>());
        Assert.NotNull(sp.GetRequiredService<IBookGenerationJournal>());
        Assert.Equal(10, sp.GetServices<IBookPipelineStep>().Count());
    }

    [Fact]
    public void Build_left_nav_contains_one_click_book_entry()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var leftNav = sp.GetRequiredService<LeftNavViewModel>();

        Assert.Contains(
            leftNav.Groups.SelectMany(group => group.Items),
            item => item.Key == PageKeys.BookPipeline && item.Label == "一键成书");
    }
}
