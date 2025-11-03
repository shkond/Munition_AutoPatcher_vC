using MunitionAutoPatcher.Services.Implementations;

namespace MunitionAutoPatcher.Models;

/// <summary>
/// Context object containing all state needed during the extraction phase.
/// </summary>
public sealed class ExtractionContext
{
    /// <summary>
    /// The Mutagen environment used for extraction.
    /// </summary>
    public IResourcedMutagenEnvironment? Environment { get; set; }

    /// <summary>
    /// LinkCache for resolving FormKeys if available.
    /// </summary>
    public object? LinkCache { get; set; }

    /// <summary>
    /// Set of plugin names to exclude from extraction.
    /// </summary>
    public HashSet<string> ExcludedPlugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All weapon records cached for the extraction run.
    /// </summary>
    public List<object> AllWeapons { get; set; } = new();

    /// <summary>
    /// Quick lookup set of weapon FormKeys (PluginName, FormID).
    /// </summary>
    public HashSet<(string Plugin, uint Id)> WeaponKeySet { get; set; } = new();

    /// <summary>
    /// Ammo records mapped by FormKey string "plugin:formid".
    /// </summary>
    public Dictionary<string, object> AmmoMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Repository root path for writing artifacts.
    /// </summary>
    public string RepoRoot { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when extraction started.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>
    /// Progress reporter for UI updates.
    /// </summary>
    public IProgress<string>? Progress { get; set; }

    /// <summary>
    /// Cancellation token for the extraction operation.
    /// </summary>
    public System.Threading.CancellationToken CancellationToken { get; set; }
}
