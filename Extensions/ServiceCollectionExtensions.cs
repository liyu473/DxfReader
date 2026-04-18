using Microsoft.Extensions.DependencyInjection;

namespace DXFReaderCore.Extensions;

/// <summary>
/// DXF 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 添加 DXF 解析服务（瞬态）
    /// </summary>
    public static IServiceCollection AddDxfParser(this IServiceCollection services)
    {
        services.AddTransient<IDxfParserService, DxfParserService>();
        return services;
    }
}
