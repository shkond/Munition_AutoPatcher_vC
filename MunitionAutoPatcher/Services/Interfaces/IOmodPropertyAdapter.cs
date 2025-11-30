using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;

namespace MunitionAutoPatcher.Services.Interfaces;

/// <summary>
/// Adapter abstraction that extracts ammo-related <see cref="FormKey"/> values from FO4 weapon OMOD properties.
/// </summary>
/// <remarks>
/// Hides Mutagen-specific APIs behind a testable interface so detectors remain stable across Mutagen versions.
/// </remarks>
public interface IOmodPropertyAdapter
{
    /// <summary>
    /// Attempts to pull the ammo <see cref="FormKey"/> from an <see cref="IAObjectModPropertyGetter{TEnum}"/>.
    /// </summary>
    /// <param name="prop">The property that already satisfied <c>Weapon.Property.Ammo</c> filtering.</param>
    /// <param name="weaponMod">Parent weapon modification (used for <see cref="ModKey"/> context).</param>
    /// <param name="formKey">Resolved FormKey when extraction succeeds.</param>
    /// <returns><c>true</c> when a valid FormKey was extracted.</returns>
    bool TryExtractFormKeyFromAmmoProperty(
        IAObjectModPropertyGetter<Weapon.Property> prop,
        IWeaponModificationGetter weaponMod,
        out FormKey formKey);
}
