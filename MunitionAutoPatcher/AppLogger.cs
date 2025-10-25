using System;
using System.Diagnostics;
using System.Threading;
using System.IO;
using System.Windows;

namespace MunitionAutoPatcher
{
    // Simple static logger used to capture previously-suppressed exceptions and forward
    // messages to Debug output and the UI MainViewModel when available. Also persists
    // a copy to artifacts/munition_autopatcher_ui.log for offline analysis.
    public static class AppLogger
    {
        // Prevent re-entrant UI forwarding which can occur when MainViewModel.AddLog
        // calls back into AppLogger.Log. Use AsyncLocal to track the forwarding state
        // per async/logical context and avoid infinite recursion (StackOverflow).
        private static readonly AsyncLocal<bool> _isForwardingToUi = new();

        public static void Log(string message, Exception? ex = null)
        {
            // 1) Write to Debug output (best-effort)
            try
            {
                if (ex == null)
                    Debug.WriteLine(message);
                else
                    Debug.WriteLine($"{message} - Exception: {ex}");
            }
            catch (Exception dbgEx)
            {
                // Avoid throwing from the logger; emit minimal fallback
                try { Debug.WriteLine($"AppLogger internal error while writing debug output: {dbgEx}"); } catch { }
            }

            // 2) Forward to UI MainViewModel.AddLog when available (best-effort)
            try
            {
                // If we're already in the middle of forwarding to the UI, skip to avoid recursion.
                if (_isForwardingToUi.Value)
                    goto persist;

                var app = Application.Current;
                if (app?.MainWindow?.DataContext is ViewModels.MainViewModel mainVm)
                {
                    var ts = DateTime.Now.ToString("HH:mm:ss");
                    var text = ex == null ? $"[{ts}] {message}" : $"[{ts}] {message} - {ex.Message}";
                    try
                    {
                        _isForwardingToUi.Value = true;
                        mainVm.AddLog(text);
                    }
                    finally
                    {
                        _isForwardingToUi.Value = false;
                    }
                }
            }
            catch (Exception uiEx)
            {
                // Avoid recursion into AppLogger.Log; write a minimal debug trace
                try { Debug.WriteLine($"AppLogger: failed to forward to UI MainViewModel: {uiEx}"); } catch { }
            }

        persist:
            // 3) Persist to artifacts log file for MO2/CI analysis (best-effort)
            try
            {
                var repoRoot = FindRepoRoot();
                var artifactsDir = Path.Combine(repoRoot, "artifacts");
                try { Directory.CreateDirectory(artifactsDir); } catch { /* best-effort */ }
                var logPath = Path.Combine(artifactsDir, "munition_autopatcher_ui.log");
                var tsFull = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var line = ex == null ? $"[{tsFull}] {message}" : $"[{tsFull}] {message} - {ex}";
                try { File.AppendAllText(logPath, line + Environment.NewLine, System.Text.Encoding.UTF8); } catch (Exception fileEx) { try { Debug.WriteLine($"AppLogger: failed to write UI log file: {fileEx}"); } catch { } }

                // Also write a fallback copy into the system temp folder so runs under MO2 or sandboxed
                // environments still produce a reachable log file.
                try
                {
                    var tmp = Path.GetTempPath();
                    var tmpName = $"munition_autopatcher_ui_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                    var tmpPath = Path.Combine(tmp, tmpName);
                    try { File.AppendAllText(tmpPath, line + Environment.NewLine, System.Text.Encoding.UTF8); } catch { }
                }
                catch { }
            }
            catch (Exception exFile)
            {
                try { Debug.WriteLine($"AppLogger: artifacts logging failed: {exFile}"); } catch { }
            }
        }

        private static string FindRepoRoot()
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    var solutionPath = Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                    if (File.Exists(solutionPath))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch { }
            return AppContext.BaseDirectory;
        }
    }
}
