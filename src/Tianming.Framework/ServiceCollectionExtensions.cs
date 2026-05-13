using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Platform;

namespace TM.Framework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrameworkServices(this IServiceCollection s)
    {
        // PortableThemeStateController 需要构造器参数（state、applyThemeAsync），
        // 在 AvaloniaShell 扩展里注册具名工厂，此处仅作预留。

        // M5：系统代理读取（shell scutil --proxy，macOS only；其它平台仍可注册，运行时
        //      因 scutil 不存在而退化为 Direct，AI 走直连，与不接代理时行为一致）。
        s.AddSingleton<IPortableSystemProxyService, MacOSSystemProxyService>();

        return s;
    }
}
