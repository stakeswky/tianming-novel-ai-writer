using Microsoft.Extensions.DependencyInjection;

namespace TM.Framework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrameworkServices(this IServiceCollection s)
    {
        // PortableThemeStateController 需要构造器参数（state、applyThemeAsync），
        // 在 AvaloniaShell 扩展里注册具名工厂，此处仅作预留。
        return s;
    }
}
