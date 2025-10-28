namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Version-agnostic detector interface for determining whether an ObjectMod/OMOD
/// changes a weapon's ammunition. Implementations may be version-specific or
/// use reflection fallback.
/// </summary>
public interface IAmmunitionChangeDetector
{
    /// <summary>
    /// Determines whether the supplied OMOD (or record) changes ammunition for the
    /// given original ammo link. Returns true and sets newAmmoLink when a change
    /// is detected.
    /// </summary>
    /// <param name="omod">An object representing the OMOD/record (version-dependent type).</param>
    /// <param name="originalAmmoLink">The original ammo FormLink object (may be null).</param>
    /// <param name="newAmmoLink">When true is returned, contains the new ammo link object.</param>
    bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink);

    /// <summary>
    /// Human-friendly name of the detector implementation (for logging/diagnostics).
    /// </summary>
    string Name { get; }
}
