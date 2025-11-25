using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Environments;
using MunitionAutoPatcher.ViewModels;
using MunitionAutoPatcher.Views;

namespace MunitionAutoPatcher;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;
    private static volatile bool _crashLogWritten = false;
    private static Exception? _lastFirstChance;
    private static volatile bool _cleanExit = false;
    private static string? _sessionMarkerPath;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
        .ConfigureLogging(logging =>
        {
            try
            {
                // ファイルベース Logger を追加（LinkResolver / AttachPointConfirmer 専用）
                var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
                var logsDir = System.IO.Path.Combine(repoRoot, "artifacts", "logs");
                var resolverLogPath = System.IO.Path.Combine(logsDir, "resolver_debug.log");
                
                logging.AddProvider(new MunitionAutoPatcher.Logging.FileLoggerProvider(resolverLogPath));

                // カテゴリフィルタ: LinkResolver と AttachPointConfirmer は Debug レベル以上をファイルに出力
                logging.AddFilter<MunitionAutoPatcher.Logging.FileLoggerProvider>(
                    "MunitionAutoPatcher.Services.Implementations.LinkResolver", 
                    LogLevel.Debug);
                logging.AddFilter<MunitionAutoPatcher.Logging.FileLoggerProvider>(
                    "MunitionAutoPatcher.Services.Implementations.AttachPointConfirmer", 
                    LogLevel.Debug);

                // AppLoggerProvider（UI 向け）: LinkResolver と AttachPointConfirmer の Debug を除外
                logging.AddFilter<MunitionAutoPatcher.Logging.AppLoggerProvider>(
                    "MunitionAutoPatcher.Services.Implementations.LinkResolver", 
                    LogLevel.Information); // Debug を除外
                logging.AddFilter<MunitionAutoPatcher.Logging.AppLoggerProvider>(
                    "MunitionAutoPatcher.Services.Implementations.AttachPointConfirmer", 
                    LogLevel.Information); // Debug を除外

                // AppLoggerProvider を追加（UI 向け）
                logging.AddProvider(new MunitionAutoPatcher.Logging.AppLoggerProvider());

                // 既存のフィルタ設定
                logging.AddFilter("MunitionAutoPatcher.Services.Implementations.MutagenV51EnvironmentAdapter", LogLevel.Debug);
                logging.AddFilter("MunitionAutoPatcher.Services.Implementations.ReverseReferenceCandidateProvider", LogLevel.Debug);
            }
            catch { }
        })
        .ConfigureServices((context, services) =>
        {
            // Register services
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<ILoadOrderService, LoadOrderService>();
            services.AddSingleton<IWeaponsService, WeaponsService>();
            // Register a factory that will create IMutagenEnvironment instances on demand.
            services.AddSingleton<IMutagenEnvironmentFactory, MutagenEnvironmentFactory>();

            // Register IResourcedMutagenEnvironment as Singleton (Mutagen v0.51 固定)
            services.AddSingleton<IResourcedMutagenEnvironment>(sp =>
            {
                var factory = sp.GetRequiredService<IMutagenEnvironmentFactory>();
                return factory.Create();
            });

            // Register IAmmunitionChangeDetector as MutagenV51Detector (Mutagen v0.51 固定)
            services.AddSingleton<IAmmunitionChangeDetector, MutagenV51Detector>();

            // Register extraction infrastructure
            services.AddSingleton<IPathService, PathService>();
            services.AddSingleton<IMutagenAccessor, MutagenAccessor>();
            services.AddSingleton<IDiagnosticWriter, DiagnosticWriter>();

            // Register candidate providers
            services.AddSingleton<ICandidateProvider, CobjCandidateProvider>();
            services.AddSingleton<ICandidateProvider, ReverseReferenceCandidateProvider>();

            // Register candidate confirmers (order: reverse-map then attach-point)
            services.AddSingleton<ICandidateConfirmer, ReverseMapConfirmer>();
            services.AddSingleton<ICandidateConfirmer, AttachPointConfirmer>();

            services.AddSingleton<IWeaponOmodExtractor, WeaponOmodExtractor>();
            // Register weapon data extractor (transient: new instance per operation)
            services.AddTransient<IWeaponDataExtractor, WeaponDataExtractor>();
            services.AddSingleton<IRobCoIniGenerator, RobCoIniGenerator>();
            services.AddSingleton<IEspPatchService, EspPatchService>();
            services.AddSingleton<IOrchestrator, OrchestratorService>();

            // Register ViewModels
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<MapperViewModel>();

            // Register Views
            services.AddSingleton<MainWindow>();
        })
        .Build();

        // Global exception handlers to capture unexpected crashes and persist diagnostics
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
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
                AppLogger.Log($"App startup debug console show failed: {ex.Message}", ex);
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
        // Create a session marker so we can detect unexpected termination on next run
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifactsDir);
            _sessionMarkerPath = Path.Combine(artifactsDir, "app_session.marker");
            File.WriteAllText(_sessionMarkerPath, $"started {DateTime.Now:u}\n");
        }
        catch { /* best-effort */ }

        // If previous session marker still exists from last run (before we just created a new one),
        // log a diagnostic to help identify silent crashes that bypass managed handlers
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            var staleMarkerPath = Path.Combine(artifactsDir, "app_session_stale.marker");
            // If there is an older stale marker, note it in the UI log for visibility
            if (File.Exists(staleMarkerPath))
            {
                AppLogger.Log("Detected stale session marker from previous run (possible hard crash). Deleting stale marker.");
                try { File.Delete(staleMarkerPath); } catch { }
            }
        }
        catch { }
        // Log the Mutagen assembly information for diagnostics (useful when running under MO2)
        try
        {
            var geType = typeof(Mutagen.Bethesda.Environments.GameEnvironment);
            var asm = geType.Assembly.GetName();
            var msg = $"Mutagen assembly loaded: {asm.Name} v{asm.Version}";
            AppLogger.Log(msg);
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
        try { DebugConsole.Hide(); } catch (Exception ex) { try { AppLogger.Log($"App exit debug console hide failed: {ex.Message}", ex); } catch (Exception inner) { AppLogger.Log("App.xaml.OnExit: failed to add log to UI on debug console hide", inner); } }
#endif
        _cleanExit = true;
        // Remove session marker on clean exit
        try
        {
            if (!string.IsNullOrEmpty(_sessionMarkerPath) && File.Exists(_sessionMarkerPath))
            {
                File.Delete(_sessionMarkerPath);
            }
        }
        catch { }
        await _host.StopAsync();
        _host.Dispose();

        base.OnExit(e);
    }

    private static void WriteCrashLog(string tag, Exception ex)
    {
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            if (!Directory.Exists(artifactsDir)) Directory.CreateDirectory(artifactsDir);

            var fileName = $"crash_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
            var path = Path.Combine(artifactsDir, fileName);
            File.WriteAllText(path, $"[{DateTime.Now:u}] {tag}: {ex}\n");
            AppLogger.Log($"Crash log written: {path}");
            _crashLogWritten = true;
        }
        catch
        {
            // Last-resort: write to console
            var fallback = Path.Combine(Path.GetTempPath(), $"munition_autopatcher_{DateTime.Now:yyyyMMdd_HHmmss_fff}_crash.log");
            try
            {
                File.WriteAllText(fallback, $"[{DateTime.Now:u}] {tag}: {ex}\n");
            }
            catch { }
            try { Console.WriteLine($"[CRASH] {tag}: {ex}"); } catch { }
        }
    }

    private static void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            var ex = e.ExceptionObject as Exception ?? new Exception("Unknown unhandled exception");
            WriteCrashLog("AppDomain.CurrentDomain.UnhandledException", ex);
        }
        catch { }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            WriteCrashLog("Application.DispatcherUnhandledException", e.Exception);
        }
        catch { }
        // Let WPF continue default behavior (can set e.Handled=true if desired)
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            WriteCrashLog("TaskScheduler.UnobservedTaskException", e.Exception);
        }
        catch { }
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        // Keep only the last first-chance exception to avoid large logs
        try { _lastFirstChance = e.Exception; } catch { }
    }

    private static void OnProcessExit(object? sender, EventArgs e)
    {
        try
        {
            var repoRoot = MunitionAutoPatcher.Utilities.RepoUtils.FindRepoRoot();
            var artifactsDir = Path.Combine(repoRoot, "artifacts");
            Directory.CreateDirectory(artifactsDir);

            // If we did not exit cleanly and no crash log was captured, emit a minimal exit diagnostic
            if (!_cleanExit && !_crashLogWritten)
            {
                var exitNote = Path.Combine(artifactsDir, $"exit_{DateTime.Now:yyyyMMdd_HHmmss_fff}_no_crashlog.log");
                try
                {
                    var lastEx = _lastFirstChance != null ? $"LastFirstChance: {_lastFirstChance.GetType().Name} - {_lastFirstChance.Message}\n{_lastFirstChance}" : "LastFirstChance: <none>";
                    File.WriteAllText(exitNote, $"[{DateTime.Now:u}] ProcessExit observed without clean exit and no crash log.\n{lastEx}\n");
                }
                catch { }

                // Preserve a stale marker to detect on next run
                try
                {
                    var stale = Path.Combine(artifactsDir, "app_session_stale.marker");
                    File.WriteAllText(stale, $"stale {DateTime.Now:u}\n");
                }
                catch { }
            }
        }
        catch { }
    }
}
