using MunitionAutoPatcher.Services.Interfaces;
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
                        var fkProp = val.GetType().GetProperty("FormKey");
                        if (fkProp == null) continue;

                        // If this property name looks like ammo/projectile, treat as candidate
                        var lname = p.Name.ToLowerInvariant();
                        if (!lname.Contains("ammo") && !lname.Contains("projectile") && !lname.Contains("bullet") && !lname.Contains("ammunition"))
                        {
                            // still consider it — some authors use uncommon names
                        }

                        // Extract FormKey info if possible
                        var fk = fkProp.GetValue(val);
                        if (fk == null) continue;

                        // If we have an original link, compare FormKeys if possible
                        if (originalAmmoLink != null)
                        {
                            try
                            {
                                var origFkProp = originalAmmoLink.GetType().GetProperty("FormKey");
                                if (origFkProp != null)
                                {
                                    var origFk = origFkProp.GetValue(originalAmmoLink);
                                    if (origFk != null && fk.ToString() == origFk.ToString())
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
