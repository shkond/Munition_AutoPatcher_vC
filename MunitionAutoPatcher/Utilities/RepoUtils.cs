using System;
using System.IO;

namespace MunitionAutoPatcher.Utilities
{
    /// <summary>
    /// Small repository-related utilities shared across services.
    /// </summary>
    public static class RepoUtils
    {
        /// <summary>
        /// Find the repository root by walking parent directories looking for the solution file.
        /// Falls back to <see cref="AppContext.BaseDirectory"/> when not found.
        /// </summary>
        public static string FindRepoRoot(string? start = null)
        {
            try
            {
                var dir = new DirectoryInfo(start ?? AppContext.BaseDirectory);
                while (dir != null)
                {
                    var solutionPath = Path.Combine(dir.FullName, "MunitionAutoPatcher.sln");
                    if (File.Exists(solutionPath))
                        return dir.FullName;
                    dir = dir.Parent;
                }
            }
            catch (Exception ex)
            {
                // Do not throw here; callers expect a best-effort path.
                try { AppLogger.Log($"RepoUtils.FindRepoRoot error: {ex.Message}"); } catch { }
            }
            return AppContext.BaseDirectory;
        }
    }
}
