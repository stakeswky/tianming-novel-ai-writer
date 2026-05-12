using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TM.Framework;
using TM.Services.Framework.AI;
using TM.Services.Modules.ProjectData;

namespace Tianming.Desktop.Avalonia;

public static class AppHost
{
    public static IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });
        s.AddProjectDataServices();
        s.AddAIServices();
        s.AddFrameworkServices();
        s.AddAvaloniaShell();
        return s.BuildServiceProvider();
    }
}
