using Microsoft.Extensions.DependencyInjection;

namespace TM.Services.Modules.ProjectData;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProjectDataServices(this IServiceCollection s)
    {
        // WritingNavigationCatalog 是 static 类，不需要注册
        // 更多 portable service 在 M4 按需加
        return s;
    }
}
