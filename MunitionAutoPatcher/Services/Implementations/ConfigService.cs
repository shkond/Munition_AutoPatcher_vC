using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

public class ConfigService : IConfigService
{
    private readonly string _configDir;
    private readonly string _configFile;
    private ConfigFile? _loaded;

    private class ConfigFile
    {
        public string GameDataPath { get; set; } = string.Empty;
        public string OutputPath { get; set; } = string.Empty;
        public StrategyConfig Strategy { get; set; } = new StrategyConfig();
        // List of plugin filenames to exclude from automatic detection (e.g. large multi-feature mods)
        public System.Collections.Generic.List<string> ExcludedPlugins { get; set; } = new System.Collections.Generic.List<string>() { "Dank_ECO.esp" };
        // UI exclusion defaults: exclude Fallout4.esm and DLC esms and cc esl by default
        public bool ExcludeFallout4Esm { get; set; } = true;
        public bool ExcludeDlcEsms { get; set; } = true;
        public bool ExcludeCcEsl { get; set; } = true;
        // Prefer EditorID display when available (default: false)
        public bool PreferEditorIdForDisplay { get; set; } = false;
    }

    public ConfigService()
    {
        // Prefer a repository-local config folder if we can locate the solution file.
        // Walk up from the application's base directory to find MunitionAutoPatcher.sln
        string? repoRoot = FindRepoRoot(AppContext.BaseDirectory);
        if (!string.IsNullOrEmpty(repoRoot))
        {
            _configDir = Path.Combine(repoRoot, "config");
        }
        else
        {
            // Fallback to user AppData
            _configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MunitionAutoPatcher");
        }
        _configFile = Path.Combine(_configDir, "config.json");
    }

    private static string? FindRepoRoot(string start)
    {
        try
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var sln = Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                if (File.Exists(sln))
                    return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch (Exception ex)
        {
            try
            {
                if (System.Windows.Application.Current?.MainWindow?.DataContext is MunitionAutoPatcher.ViewModels.MainViewModel mainVm)
                    mainVm.AddLog($"ConfigService.FindRepoRoot error: {ex.Message}");
            }
            catch (Exception ex2) { AppLogger.Log("ConfigService: failed to add log to UI in FindRepoRoot", ex2); }
        }
        return null;
    }

    private void EnsureLoaded()
    {
        if (_loaded != null) return;

        try
        {
            if (File.Exists(_configFile))
            {
                var txt = File.ReadAllText(_configFile);
                _loaded = JsonSerializer.Deserialize<ConfigFile>(txt) ?? new ConfigFile();
            }
            else
            {
                _loaded = new ConfigFile();
            }
        }
        catch (Exception ex)
        {
            // Loading the config failed — do NOT silently overwrite the user's configuration.
            AppLogger.Log($"ConfigService: failed to load config file '{_configFile}'", ex);
            // Surface the error to the caller so it can decide how to proceed (avoids silently creating defaults).
            throw;
        }
    }

    public Task<StrategyConfig> LoadConfigAsync()
    {
        EnsureLoaded();
        return Task.FromResult(_loaded!.Strategy);
    }

    public async Task SaveConfigAsync(StrategyConfig config)
    {
        EnsureLoaded();
        _loaded!.Strategy = config;
        try
        {
            if (!Directory.Exists(_configDir)) Directory.CreateDirectory(_configDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var txt = JsonSerializer.Serialize(_loaded, options);
            await File.WriteAllTextAsync(_configFile, txt);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"ConfigService: failed to save config file '{_configFile}'", ex);
            // Surface failure to caller so UI can notify user or retry.
            throw;
        }
    }

    public string GetGameDataPath()
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(_loaded!.GameDataPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) ?? "C:\\Program Files (x86)", "Steam", "steamapps", "common", "Fallout 4", "Data")
            : _loaded.GameDataPath;
    }

    public void SetGameDataPath(string path)
    {
        EnsureLoaded();
        _loaded!.GameDataPath = path;
        // save asynchronously in background
        _ = SaveAllAsync();
    }

    public string GetOutputPath()
    {
        EnsureLoaded();
        return string.IsNullOrEmpty(_loaded!.OutputPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) ?? Environment.CurrentDirectory, "RobCoPatcher.ini")
            : _loaded.OutputPath;
    }

    public void SetOutputPath(string path)
    {
        EnsureLoaded();
        _loaded!.OutputPath = path;
        _ = SaveAllAsync();
    }

    public bool GetExcludeFallout4Esm()
    {
        EnsureLoaded();
        return _loaded!.ExcludeFallout4Esm;
    }

    public void SetExcludeFallout4Esm(bool v)
    {
        EnsureLoaded();
        _loaded!.ExcludeFallout4Esm = v;
        _ = SaveAllAsync();
    }

    public bool GetExcludeDlcEsms()
    {
        EnsureLoaded();
        return _loaded!.ExcludeDlcEsms;
    }

    public void SetExcludeDlcEsms(bool v)
    {
        EnsureLoaded();
        _loaded!.ExcludeDlcEsms = v;
        _ = SaveAllAsync();
    }

    public bool GetExcludeCcEsl()
    {
        EnsureLoaded();
        return _loaded!.ExcludeCcEsl;
    }

    public void SetExcludeCcEsl(bool v)
    {
        EnsureLoaded();
        _loaded!.ExcludeCcEsl = v;
        _ = SaveAllAsync();
    }

    public bool GetPreferEditorIdForDisplay()
    {
        EnsureLoaded();
        return _loaded!.PreferEditorIdForDisplay;
    }

    public void SetPreferEditorIdForDisplay(bool v)
    {
        EnsureLoaded();
        _loaded!.PreferEditorIdForDisplay = v;
        _ = SaveAllAsync();
    }

    public System.Collections.Generic.IEnumerable<string> GetExcludedPlugins()
    {
        EnsureLoaded();
        return _loaded!.ExcludedPlugins ?? new System.Collections.Generic.List<string>();
    }

    public void SetExcludedPlugins(System.Collections.Generic.IEnumerable<string> plugins)
    {
        EnsureLoaded();
        _loaded!.ExcludedPlugins = new System.Collections.Generic.List<string>(plugins ?? System.Array.Empty<string>());
        _ = SaveAllAsync();
    }

    private async Task SaveAllAsync()
    {
        try
        {
            if (!Directory.Exists(_configDir)) Directory.CreateDirectory(_configDir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            var txt = JsonSerializer.Serialize(_loaded, options);
            await File.WriteAllTextAsync(_configFile, txt);
        }
        catch (Exception ex)
        {
            AppLogger.Log($"ConfigService: failed to persist configuration to '{_configFile}'", ex);
            throw;
        }
    }
}
