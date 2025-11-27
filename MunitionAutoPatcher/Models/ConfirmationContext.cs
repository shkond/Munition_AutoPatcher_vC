using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Plugins.Cache;

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
    /// <remarks>
    /// This collection contains ONLY winning overrides from the load order,
    /// not all weapon records across all plugins. Populated by 
    /// <c>IMutagenAccessor.GetWinningWeaponOverrides()</c> which internally calls
    /// <c>loadOrder.PriorityOrder.Weapon().WinningOverrides()</c>.
    /// This ensures optimal performance and accuracy by using only the final,
    /// effective weapon records as they appear in-game.
    /// </remarks>
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
    public ILinkResolver? Resolver { get; set; }

    /// <summary>
    /// The LinkCache for the confirmation phase.
    /// </summary>
    public ILinkCache? LinkCache { get; set; }

    /// <summary>
    /// Cancellation token for the confirmation operation.
    /// </summary>
    public System.Threading.CancellationToken CancellationToken { get; set; }
}
