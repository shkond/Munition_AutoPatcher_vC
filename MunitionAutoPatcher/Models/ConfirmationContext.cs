using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Models;

/// <summary>
/// Context object containing all state needed during the confirmation phase.
/// </summary>
public sealed class ConfirmationContext
{
    /// <summary>
    /// Reverse-reference map: FormKey string -> list of records that reference it.
    /// </summary>
    public Dictionary<string, List<(object Record, string PropName, object PropValue)>> ReverseMap { get; set; } 
        = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Set of plugin names to exclude from confirmation.
    /// </summary>
    public HashSet<string> ExcludedPlugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// All weapon records for lookup.
    /// </summary>
    public List<object> AllWeapons { get; set; } = new();

    /// <summary>
    /// Ammo records mapped by FormKey string "plugin:formid".
    /// </summary>
    public Dictionary<string, object>? AmmoMap { get; set; }

    /// <summary>
    /// Detector for checking if an OMOD changes ammo.
    /// </summary>
    public IAmmunitionChangeDetector? Detector { get; set; }

    /// <summary>
    /// LinkResolver for resolving FormKeys.
    /// </summary>
    public LinkResolver? Resolver { get; set; }

    /// <summary>
    /// LinkCache for fallback resolution.
    /// </summary>
    public object? LinkCache { get; set; }

    /// <summary>
    /// Cancellation token for the confirmation operation.
    /// </summary>
    public System.Threading.CancellationToken CancellationToken { get; set; }
}
