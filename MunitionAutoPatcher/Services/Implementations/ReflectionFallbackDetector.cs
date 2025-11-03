using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// A conservative reflection-based detector that attempts to find ammo-like
/// FormLink fields on the supplied OMOD/record. This is slow but maximally
/// compatible across Mutagen versions.
/// </summary>
public class ReflectionFallbackDetector : IAmmunitionChangeDetector
{
    public string Name => "ReflectionFallbackDetector";

    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        if (omod == null) return false;

        try
        {
            var props = omod.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in props)
            {
                    try
                    {
                        // Heuristic: property with FormLink-like shape (has FormKey property)
                        var val = p.GetValue(omod);
                        if (val == null) continue;
                        if (!MutagenReflectionHelpers.TryGetFormKey(val, out var candidateFormKey) || candidateFormKey == null)
                            continue;

                        // If this property name looks like ammo/projectile, treat as candidate
                        var lname = p.Name.ToLowerInvariant();
                        if (!lname.Contains("ammo") && !lname.Contains("projectile") && !lname.Contains("bullet") && !lname.Contains("ammunition"))
                        {
                            // still consider it — some authors use uncommon names
                        }

                        // If we have an original link, compare FormKeys if possible
                        if (originalAmmoLink != null)
                        {
                            try
                            {
                                if (MutagenReflectionHelpers.TryGetFormKey(originalAmmoLink, out var originalFormKey) && originalFormKey != null)
                                {
                                    if (string.Equals(candidateFormKey.ToString(), originalFormKey.ToString(), StringComparison.Ordinal))
                                    {
                                        // same -> not a change
                                        continue;
                                    }
                                }
                            }
                            catch (Exception ex) { AppLogger.Log("ReflectionFallbackDetector: failed comparing original FormKey", ex); }
                        }

                        // Otherwise, consider this a new ammo link
                        newAmmoLink = val;
                        return true;
                    }
                    catch (Exception ex) { AppLogger.Log("ReflectionFallbackDetector: property inspection failed", ex); /* swallow and continue */ }
            }
        }
    catch (Exception ex) { AppLogger.Log("ReflectionFallbackDetector: top-level reflection inspection failed", ex); }

        return false;
    }
}
