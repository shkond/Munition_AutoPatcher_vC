using System.Reflection;
using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Confirmer that derives confirmation evidence from OMOD AttachPoint ↔ Weapon AttachParentSlots
/// matching combined with detection of ammo/projectile links inside the OMOD (or related created object).
/// This does not rely on the reverse-reference map (which is empty for FO4 attach point semantics).
/// </summary>
public sealed class AttachPointConfirmer : ICandidateConfirmer
{
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly ILogger<AttachPointConfirmer> _logger;

    public AttachPointConfirmer(IMutagenAccessor mutagenAccessor, ILogger<AttachPointConfirmer> logger)
    {
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Confirm(IEnumerable<OmodCandidate> candidates, ConfirmationContext context)
    {
        // Build quick lookup: weapon attach slot keyword keys -> weapon FormKey
        var attachSlotToWeapon = new Dictionary<(string Plugin, uint Id), List<FormKey>>();
        foreach (var weapon in context.AllWeapons)
        {
            try
            {
                if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var wPlugin, out var wId) || string.IsNullOrEmpty(wPlugin) || wId == 0)
                    continue;
                var fkWeapon = new FormKey { PluginName = wPlugin, FormId = wId };
                var apsProp = weapon.GetType().GetProperty("AttachParentSlots");
                var apsVal = apsProp?.GetValue(weapon) as System.Collections.IEnumerable;
                if (apsVal == null) continue;
                foreach (var link in apsVal)
                {
                    if (link == null) continue;
                    var fkProp = link.GetType().GetProperty("FormKey");
                    var fk = fkProp?.GetValue(link);
                    if (fk != null && TryExtractFormKeyInfo(fk, out var p, out var id))
                    {
                        var key = (p.ToLowerInvariant(), id);
                        if (!attachSlotToWeapon.TryGetValue(key, out var list))
                        {
                            list = new List<FormKey>();
                            attachSlotToWeapon[key] = list;
                        }
                        list.Add(fkWeapon);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AttachPointConfirmer: error while building weapon slot map");
            }
        }

        int processed = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                // Skip already confirmed
                if (candidate.ConfirmedAmmoChange) continue;

                // We only handle candidates that represent potential OMOD or created weapon modifications
                if (!IsOmodLike(candidate)) continue;

                // Resolve OMOD getter from CandidateFormKey
                object? omodGetter = null;
                try
                {
                    omodGetter = context.Resolver?.ResolveByKey(candidate.CandidateFormKey);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "AttachPointConfirmer: failed to resolve candidate form key");
                }
                if (omodGetter == null) continue;

                // Extract attach point keyword link
                object? apLink = TryFindAttachPointLink(omodGetter);
                if (apLink == null)
                    continue; // cannot map weapon applicability

                var fkProp = apLink.GetType().GetProperty("FormKey");
                var fk = fkProp?.GetValue(apLink);
                if (fk == null || !TryExtractFormKeyInfo(fk, out var apPlugin, out var apId))
                    continue;
                var apKey = (apPlugin.ToLowerInvariant(), apId);

                if (!attachSlotToWeapon.TryGetValue(apKey, out var affectedWeapons) || affectedWeapons.Count == 0)
                    continue; // attach point not used by any loaded weapon

                // Attempt to locate ammo/proj reference inside the OMOD
                var ammoFormKey = TryFindAmmoReference(omodGetter, context);
                if (ammoFormKey == null)
                    continue; // We only confirm when ammo evidence is found

                // Confirm for each weapon affected; if candidate already tied to BaseWeapon keep it, else assign first
                if (candidate.BaseWeapon == null)
                {
                    candidate.BaseWeapon = affectedWeapons[0];
                    candidate.BaseWeaponEditorId = _mutagenAccessor.GetEditorId(affectedWeapons[0]);
                }

                candidate.CandidateAmmo = ammoFormKey;
                candidate.CandidateAmmoName = context.Resolver != null ? _mutagenAccessor.GetEditorId(context.Resolver.ResolveByKey(ammoFormKey) ?? new object()) : string.Empty;
                candidate.ConfirmedAmmoChange = true;
                candidate.ConfirmReason = $"AttachPointMatch+Ammo ({apPlugin}:{apId:X8})";
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AttachPointConfirmer: error confirming candidate");
            }
            processed++;
        }

        _logger.LogInformation("AttachPointConfirmer processed {Count} candidates", processed);
    }

    private static bool IsOmodLike(OmodCandidate c)
    {
        if (c == null) return false;
        var type = c.CandidateType.ToLowerInvariant();
        return type.Contains("omod") || type.Contains("objectmodification") || type.Contains("objectmod") || type.Contains("cobj") || type.Contains("createdweapon");
    }

    private object? TryFindAttachPointLink(object omodGetter)
    {
        try
        {
            var props = omodGetter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                var name = prop.Name;
                if (name.Equals("AttachPoint", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("AttachParentSlot", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("AttachPoint", StringComparison.OrdinalIgnoreCase))
                {
                    var v = prop.GetValue(omodGetter);
                    if (v != null) return v;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AttachPointConfirmer: failed to scan for attach point link");
        }
        return null;
    }

    private FormKey? TryFindAmmoReference(object omodGetter, ConfirmationContext context)
    {
        try
        {
            var props = omodGetter.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.GetIndexParameters().Length > 0) continue;
                object? value = null;
                try { value = prop.GetValue(omodGetter); } catch { continue; }
                if (value == null) continue;

                // Direct link value
                var fkProp = value.GetType().GetProperty("FormKey");
                if (fkProp != null)
                {
                    var fk = fkProp.GetValue(value);
                    if (fk != null && TryExtractFormKeyInfo(fk, out var plugin, out var id))
                    {
                        var keyStr = $"{plugin}:{id:X8}";
                        if (context.AmmoMap != null && context.AmmoMap.ContainsKey(keyStr))
                        {
                            return new FormKey { PluginName = plugin, FormId = id };
                        }
                    }
                }

                // Enumerable case (scan first few elements)
                if (value is System.Collections.IEnumerable seq)
                {
                    int inspected = 0;
                    foreach (var elem in seq)
                    {
                        if (elem == null) continue;
                        inspected++;
                        if (inspected > 16) break;
                        var fkPropElem = elem.GetType().GetProperty("FormKey");
                        if (fkPropElem == null) continue;
                        var fkElem = fkPropElem.GetValue(elem);
                        if (fkElem == null) continue;
                        if (TryExtractFormKeyInfo(fkElem, out var p2, out var id2))
                        {
                            var keyStr2 = $"{p2}:{id2:X8}";
                            if (context.AmmoMap != null && context.AmmoMap.ContainsKey(keyStr2))
                            {
                                return new FormKey { PluginName = p2, FormId = id2 };
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AttachPointConfirmer: failed scanning OMOD for ammo reference");
        }
        return null;
    }

    private static bool TryExtractFormKeyInfo(object formKey, out string plugin, out uint id)
    {
        plugin = string.Empty;
        id = 0;
        try
        {
            var modKey = formKey.GetType().GetProperty("ModKey")?.GetValue(formKey);
            if (modKey == null) return false;
            var idObj = formKey.GetType().GetProperty("ID")?.GetValue(formKey);
            if (idObj == null) return false;
            var fileNameObj = modKey.GetType().GetProperty("FileName")?.GetValue(modKey);
            plugin = (fileNameObj?.ToString() ?? modKey.ToString()) ?? string.Empty;
            id = idObj is uint ui ? ui : Convert.ToUInt32(idObj);
            return !string.IsNullOrEmpty(plugin) && !plugin.Equals("Null", StringComparison.OrdinalIgnoreCase) && id != 0;
        }
        catch { return false; }
    }
}