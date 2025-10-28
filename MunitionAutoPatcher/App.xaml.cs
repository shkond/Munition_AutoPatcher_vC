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
                services.AddSingleton<IWeaponOmodExtractor, WeaponOmodExtractor>();
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
        // Ensure support for legacy code page encodings (Shift-JIS, etc.) used by some game plugins.
        // Call once at startup so Encoding.GetEncoding(...) works for code pages.
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
#if DEBUG
        // In Debug builds, pause at startup to allow attaching a debugger (useful when launching via MO2)
        try
        {
            DebugConsole.Show();
            Console.WriteLine("--- DEBUG MODE ---");
            Console.WriteLine("デバッガをアタッチしてください。アタッチ後、Enterキーを押すと処理を続行します...");
            Console.ReadLine();
        }
        catch (Exception ex)
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                    mainVm.AddLog($"App startup debug console show failed: {ex.Message}");
                else
                    Console.WriteLine($"App startup debug console show failed: {ex.Message}");
            }
            catch { Console.WriteLine($"App startup debug console show failed: {ex.Message}"); }
        }
#endif

        try
        {
            await _host.StartAsync();
        }
        catch (Exception ex)
        {
            // Always log a summary; in DEBUG builds, print full exception
            Console.WriteLine($"ホストの起動中にエラーが発生しました: {ex.Message}");
#if DEBUG
            Console.WriteLine(ex.ToString());
#endif
            throw;
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        // Log the Mutagen assembly information for diagnostics (useful when running under MO2)
            try
            {
                var geType = typeof(Mutagen.Bethesda.Environments.GameEnvironment);
                var asm = geType.Assembly.GetName();
                var msg = $"Mutagen assembly loaded: {asm.Name} v{asm.Version}";
                AppLogger.Log(msg);
                    try { mainViewModel.AddLog(msg); } catch (Exception ex) { AppLogger.Log("App.xaml: failed to add startup log to UI", ex); }
            }
            catch (Exception ex)
            {
                AppLogger.Log("Failed to detect Mutagen assembly at startup", ex);
            }

        mainWindow.DataContext = mainViewModel;
        mainWindow.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
#if DEBUG
    try { DebugConsole.Hide(); } catch (Exception ex) { try { if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm) mainVm.AddLog($"App exit debug console hide failed: {ex.Message}"); } catch (Exception inner) { AppLogger.Log("App.xaml.OnExit: failed to add log to UI on debug console hide", inner); } }
#endif
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }
}
