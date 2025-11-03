using MunitionAutoPatcher.Services.Interfaces;
using MunitionAutoPatcher.Utilities;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Mutagen v0.51-aware detector. This implementation tries lightweight,
/// version-specific heuristics first (type-name checks and common property
/// names). If they fail, it falls back to the reflection-based detector.
/// </summary>
public class MutagenV51Detector : IAmmunitionChangeDetector
{
    private readonly ReflectionFallbackDetector _fallback = new ReflectionFallbackDetector();

    public string Name => "MutagenV51Detector";

    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        newAmmoLink = null;
        if (omod == null) return false;

        try
        {
            // Fast-path: if the runtime type name matches known Mutagen v0.51 objectmod types,
            // inspect the most-likely properties by name for ammo/projectile links.
            var tname = omod.GetType().Name ?? string.Empty;
            var lname = tname.ToLowerInvariant();

            // Known candidates in older Mutagen: ObjectMod*, Weapon*, ConstructibleObject*, etc.
            if (lname.Contains("objectmod") || lname.Contains("object_mod") || lname.Contains("objectmodentry") || lname.Contains("constructible") || lname.Contains("cobj") || lname.Contains("weapon") || lname.Contains("object"))
            {
                var props = omod.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                // Prefer properties which contain ammo-like names
                    foreach (var p in props.OrderByDescending(p => IsAmmoLikeName(p.Name)))
                {
                    try
                    {
                        if (!IsAmmoLikeName(p.Name) && !IsLikelyFormLinkProperty(p))
                            continue;

                        var val = p.GetValue(omod);
                        if (val == null) continue;

                        // 標準化されたヘルパーでFormKeyを取得
                        if (!MutagenReflectionHelpers.TryGetFormKey(val, out var candidateFormKey) || candidateFormKey == null)
                            continue;

                        // 元の弾薬リンクが渡されている場合、FormKey同一なら変更なしとしてスキップ
                        if (originalAmmoLink != null)
                        {
                            try
                            {
                                if (MutagenReflectionHelpers.TryGetFormKey(originalAmmoLink, out var originalFormKey) && originalFormKey != null)
                                {
                                    if (string.Equals(candidateFormKey.ToString(), originalFormKey.ToString(), StringComparison.Ordinal))
                                    {
                                        // same as original -> not a change
                                        continue;
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                AppLogger.Log("MutagenV51Detector: failing to read original FormKey during detection", ex);
                            }
                        }

                        newAmmoLink = val;
                        return true;
                    }
                    catch (Exception ex)
                    {
                        // Log the property-level error but continue scanning other properties.
                        AppLogger.Log($"MutagenV51Detector: error inspecting property '{p.Name}' on type {omod.GetType().FullName}", ex);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenV51Detector: fast-path detection failed, falling back to reflection detector", ex);
        }

        // Fallback to the resilient reflection detector
        try
        {
            return _fallback.DoesOmodChangeAmmo(omod, originalAmmoLink, out newAmmoLink);
        }
        catch (Exception ex)
        {
            AppLogger.Log("MutagenV51Detector: reflection fallback detector threw an exception", ex);
            newAmmoLink = null;
            return false;
        }
    }

    private static bool IsAmmoLikeName(string name)
    {
        var n = name.ToLowerInvariant();
        return n.Contains("ammo") || n.Contains("projectile") || n.Contains("bullet") || n.Contains("ammunition") || n.Contains("projectilegetter");
    }

    private static bool IsLikelyFormLinkProperty(PropertyInfo p)
    {
        // Heuristic: if the property's type exposes a FormKey property, it's worth inspecting
        try
        {
            var t = p.PropertyType;
            return t.GetProperty("FormKey") != null;
        }
        catch (Exception ex)
        {
            AppLogger.Log($"MutagenV51Detector.IsLikelyFormLinkProperty: failed for property {p.Name}", ex);
            return false;
        }
    }
}
