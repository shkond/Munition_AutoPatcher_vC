using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.ViewModels;
using MunitionAutoPatcher.Views;

namespace MunitionAutoPatcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register services
                services.AddSingleton<IConfigService, ConfigService>();
                services.AddSingleton<ILoadOrderService, LoadOrderService>();
                services.AddSingleton<IWeaponsService, WeaponsService>();
                services.AddSingleton<IRobCoIniGenerator, RobCoIniGenerator>();
                services.AddSingleton<IOrchestrator, OrchestratorService>();

                // Register ViewModels
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<MapperViewModel>();

                // Register Views
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        
        mainWindow.DataContext = mainViewModel;
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
