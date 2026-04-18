using DXFReaderCore;
using DXFReaderDemo.ViewModels;
using DXFReaderDemo.Views;
using LyuWpfHelper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace DXFReaderDemo;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 注册日志
                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddDebug();
                });

                // 注册 LyuWpfHelper 服务
                services.AddSingleton<IBusyService, BusyService>();

                // 注册 DXF 服务
                services.AddSingleton<IDxfParserService, DxfParserService>();

                // 注册 ViewModels
                services.AddSingleton<DxfReaderViewModel>();

                // 注册 Views
                services.AddSingleton<DxfReaderView>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}

