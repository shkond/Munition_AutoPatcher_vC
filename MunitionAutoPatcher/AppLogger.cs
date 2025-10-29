using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Collections.Concurrent;
using MunitionAutoPatcher.Utilities;

namespace MunitionAutoPatcher
{
    // Simple static logger used to capture previously-suppressed exceptions and forward
    // messages to Debug output and the UI MainViewModel when available. Also persists
    // a copy to artifacts/munition_autopatcher_ui.log for offline analysis.
    public static class AppLogger
    {
        // Event-based logging: publish formatted log lines to subscribers.
        // This centralizes UI updates and keeps components decoupled.
        public static event Action<string>? LogMessagePublished;

    // Prevent re-entrant forwarding storms. Use an interlocked flag process-wide.
    private static int _isForwardingToUiFlag = 0;

    // Background queue for async file writes to avoid synchronous disk I/O on caller threads.
    private static readonly ConcurrentQueue<string> _fileWriteQueue = new();
    private static readonly SemaphoreSlim _queueSignal = new(0);
    private static readonly CancellationTokenSource _cts = new();
    private static readonly Task _backgroundWriterTask;

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

            // 2) Publish a log event so subscribers (e.g. MainViewModel) can update UI.
            try
            {
                var ts = DateTime.Now.ToString("HH:mm:ss");
                var text = ex == null ? $"[{ts}] {message}" : $"[{ts}] {message} - {ex.Message}";

                // Attempt to acquire guard; if another thread is already publishing, skip to persist
                if (Interlocked.CompareExchange(ref _isForwardingToUiFlag, 1, 0) == 0)
                {
                    try
                    {
                        var h = LogMessagePublished;
                        if (h != null)
                        {
                            foreach (var d in h.GetInvocationList())
                            {
                                if (d is Action<string> act)
                                {
                                    Task.Run(() => { try { act(text); } catch { /* swallow */ } });
                                }
                            }
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isForwardingToUiFlag, 0);
                    }
                }
            }
            catch (Exception uiEx)
            {
                try { Debug.WriteLine($"AppLogger: failed to publish log event: {uiEx}"); } catch { }
            }

            // 3) Persist to artifacts log file for MO2/CI analysis (best-effort) via background queue
            try
            {
                var tsFull = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var line = ex == null ? $"[{tsFull}] {message}" : $"[{tsFull}] {message} - {ex}";
                EnqueueFileWrite(line);
            }
            catch (Exception exFile)
            {
                try { Debug.WriteLine($"AppLogger: artifacts logging enqueue failed: {exFile}"); } catch { }
            }
        }

        static AppLogger()
        {
            // Start background writer task
            _backgroundWriterTask = Task.Run(() => BackgroundWriterLoopAsync(_cts.Token));

            // Ensure we attempt to flush on process exit
            try
            {
                AppDomain.CurrentDomain.ProcessExit += (s, e) => StopBackgroundWriter();
            }
            catch { }
        }

        private static void EnqueueFileWrite(string line)
        {
            try
            {
                _fileWriteQueue.Enqueue(line);
                _queueSignal.Release();
            }
            catch (Exception qEx)
            {
                try { Debug.WriteLine($"AppLogger: failed to enqueue log line: {qEx}"); } catch { }
            }
        }

        private static async Task BackgroundWriterLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await _queueSignal.WaitAsync(ct).ConfigureAwait(false);

                    // Dequeue all available items and write them in a single open/close
                    var lines = new System.Collections.Generic.List<string>();
                    while (_fileWriteQueue.TryDequeue(out var l))
                    {
                        lines.Add(l);
                    }

                    if (lines.Count == 0) continue;

                    try
                    {
                        var repoRoot = RepoUtils.FindRepoRoot();
                        var artifactsDir = Path.Combine(repoRoot, "artifacts");
                        try { Directory.CreateDirectory(artifactsDir); } catch { }
                        var logPath = Path.Combine(artifactsDir, "munition_autopatcher_ui.log");

                        // Use async file I/O to append all lines
                        try
                        {
                            using (var fs = new FileStream(logPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true))
                            using (var sw = new StreamWriter(fs, System.Text.Encoding.UTF8))
                            {
                                foreach (var line in lines)
                                {
                                    await sw.WriteLineAsync(line).ConfigureAwait(false);
                                }
                                await sw.FlushAsync().ConfigureAwait(false);
                            }
                        }
                        catch (Exception fileEx)
                        {
                            try { Debug.WriteLine($"AppLogger: background write failed: {fileEx}"); } catch { }
                        }

                        // Also try to write a temporary per-run copy to temp folder (best-effort)
                        try
                        {
                            var tmp = Path.GetTempPath();
                            var tmpName = $"munition_autopatcher_ui_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                            var tmpPath = Path.Combine(tmp, tmpName);
                            try
                            {
                                using (var fs2 = new FileStream(tmpPath, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, useAsync: true))
                                using (var sw2 = new StreamWriter(fs2, System.Text.Encoding.UTF8))
                                {
                                    foreach (var line in lines)
                                    {
                                        await sw2.WriteLineAsync(line).ConfigureAwait(false);
                                    }
                                    await sw2.FlushAsync().ConfigureAwait(false);
                                }
                            }
                            catch { }
                        }
                        catch { }
                    }
                    catch (Exception ex)
                    {
                        try { Debug.WriteLine($"AppLogger: BackgroundWriterLoop caught: {ex}"); } catch { }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception outer)
                {
                    try { Debug.WriteLine($"AppLogger: BackgroundWriterLoop outer exception: {outer}"); } catch { }
                }
            }

            // Drain remaining items synchronously on shutdown
            try
            {
                var remaining = new System.Collections.Generic.List<string>();
                while (_fileWriteQueue.TryDequeue(out var l)) remaining.Add(l);
                if (remaining.Count > 0)
                {
                    var repoRoot = RepoUtils.FindRepoRoot();
                    var artifactsDir = Path.Combine(repoRoot, "artifacts");
                    try { Directory.CreateDirectory(artifactsDir); } catch { }
                    var logPath = Path.Combine(artifactsDir, "munition_autopatcher_ui.log");
                    try
                    {
                        File.AppendAllLines(logPath, remaining, System.Text.Encoding.UTF8);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static void StopBackgroundWriter()
        {
            try
            {
                _cts.Cancel();
                try { _backgroundWriterTask?.Wait(2000); } catch { }
            }
            catch { }
        }

        // Repo root lookup centralized in MunitionAutoPatcher.Utilities.RepoUtils
    }
}
