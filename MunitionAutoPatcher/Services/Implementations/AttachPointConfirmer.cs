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

    public async Task ConfirmAsync(IEnumerable<OmodCandidate> candidates, ConfirmationContext context, CancellationToken cancellationToken)
    {
        // Dump first few candidate form keys for debugging
        try
        {
            int dumpCount = 0;
            foreach (var c in candidates.Take(20))
            {
                try
                {
                    var fk = c.CandidateFormKey;
                    _logger.LogInformation("CandidateDump[{Index}]: Type={Type} FK={Plugin}:{Id:X8}", dumpCount++, c.CandidateType, fk?.PluginName ?? "NULL", fk?.FormId ?? 0);
                }
                catch { /* best-effort debug only */ }
            }
        }
        catch { /* ignore debug failures */ }

        // Quick check: try resolving a known vanilla FormKey
        try
        {
            if (context.LinkCache != null)
            {
                var testKey = new Mutagen.Bethesda.Plugins.FormKey(
                    new Mutagen.Bethesda.Plugins.ModKey("Fallout4.esm", Mutagen.Bethesda.Plugins.ModType.Master),
                    0x0004D00C); // Known vanilla OMOD
                if (context.LinkCache.TryResolve<Mutagen.Bethesda.Fallout4.IObjectModificationGetter>(testKey, out var testOmod) && testOmod != null)
                {
                    _logger.LogInformation("QuickCheck: Successfully resolved vanilla OMOD Fallout4.esm:0004D00C");
                }
                else
                {
                    _logger.LogWarning("QuickCheck: FAILED to resolve vanilla OMOD Fallout4.esm:0004D00C - LinkCache may be incomplete");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "QuickCheck: Exception during vanilla FormKey test");
        }

        // Build quick lookup: weapon attach slot keyword keys -> weapon FormKey
        var attachSlotToWeapon = new Dictionary<(string Plugin, uint Id), List<FormKey>>();
        foreach (var weapon in context.AllWeapons)
        {
            try
            {
                if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var wPlugin, out var wId) || string.IsNullOrEmpty(wPlugin) || wId == 0)
                    continue;
                var fkWeapon = new FormKey { PluginName = wPlugin, FormId = wId };
                var apsProp = weapon.GetType().GetProperty("AttachParentSlots", BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
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

        int inspected = 0;
        int resolvedToOmod = 0;
        int hadAttachPoint = 0;
        int matchedWeapons = 0;
        int foundAmmo = 0;
        int confirmed = 0;
        // debugging counters for resolution failures
        int rootNull = 0, createdObjMissing = 0, createdObjResolveFail = 0, createdObjNotOmod = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                inspected++;
                // Skip already confirmed
                if (candidate.ConfirmedAmmoChange) continue;

                // We only handle candidates that represent potential OMOD or created weapon modifications
                if (!IsOmodLike(candidate)) continue;

                // Resolve OMOD getter from CandidateFormKey (follow through COBJ.CreatedObject when needed)
                object? omodGetter = ResolveOmodForCandidate(candidate, context, ref rootNull, ref createdObjMissing, ref createdObjResolveFail, ref createdObjNotOmod);
                if (omodGetter == null) continue;
                resolvedToOmod++;

                // Extract attach point keyword link
                object? apLink = TryFindAttachPointLink(omodGetter);
                if (apLink == null)
                    continue; // cannot map weapon applicability
                hadAttachPoint++;

                var fkProp = apLink.GetType().GetProperty("FormKey");
                var fk = fkProp?.GetValue(apLink);
                if (fk == null || !TryExtractFormKeyInfo(fk, out var apPlugin, out var apId))
                    continue;
                var apKey = (apPlugin.ToLowerInvariant(), apId);

                if (!attachSlotToWeapon.TryGetValue(apKey, out var affectedWeapons) || affectedWeapons.Count == 0)
                    continue; // attach point not used by any loaded weapon
                matchedWeapons += affectedWeapons.Count;

                // Attempt to locate ammo/proj reference inside the OMOD
                var ammoFormKey = TryFindAmmoReference(omodGetter, context);
                if (ammoFormKey == null)
                    continue; // We only confirm when ammo evidence is found
                foundAmmo++;

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
                confirmed++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "AttachPointConfirmer: error confirming candidate");
            }
        }

        _logger.LogInformation(
            "AttachPointConfirmer: inspected={Inspected}, resolvedToOmod={Resolved}, hadAttachPoint={AttachPts}, matchedWeapons={Matched}, foundAmmo={Ammo}, confirmed={Confirmed}",
            inspected, resolvedToOmod, hadAttachPoint, matchedWeapons, foundAmmo, confirmed);
        
        _logger.LogInformation(
            "AttachPointConfirmer: failures rootNull={RootNull}, createdObjMissing={CreatedMissing}, createdObjResolveFail={ResolveFail}, createdObjNotOmod={NotOmod}",
            rootNull, createdObjMissing, createdObjResolveFail, createdObjNotOmod);

        // Log sample of rootNull failures for diagnosis
        if (rootNull > 0)
        {
            try
            {
                int logged = 0;
                foreach (var c in candidates.Where(x => !x.ConfirmedAmmoChange).Take(10))
                {
                    _logger.LogInformation("RootNull sample[{Index}]: Type={Type} FK={Plugin}:{Id:X8}",
                        logged++, c.CandidateType, c.CandidateFormKey?.PluginName ?? "NULL", c.CandidateFormKey?.FormId ?? 0);
                }
            }
            catch { /* best-effort */ }
        }
    }

    private static bool IsOmodLike(OmodCandidate c)
    {
        if (c == null) return false;
        var type = c.CandidateType.ToLowerInvariant();
        return type.Contains("omod")
            || type.Contains("objectmodification")
            || type.Contains("objectmod")
            || type.Contains("cobj")
            || type.Contains("constructibleobject")
            || type.Contains("createdweapon");
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

    private object? ResolveOmodForCandidate(OmodCandidate candidate, ConfirmationContext context,
        ref int rootNull, ref int createdObjMissing, ref int createdObjResolveFail, ref int createdObjNotOmod)
    {
        object? root = null;
        try
        {
            _logger.LogDebug("ResolveOmod: CandidateType={Type} CandidateFormKey={Plugin}:{Id:X8}",
                candidate.CandidateType,
                candidate.CandidateFormKey.PluginName,
                candidate.CandidateFormKey.FormId);

            // Prefer resolving via a concrete Mutagen FormKey when possible
            try
            {
                var mfk = ToMutagenFormKey(candidate.CandidateFormKey);
                if (mfk != null && context.Resolver != null && context.Resolver.TryResolve(mfk.Value, out var resolved) && resolved != null)
                {
                    root = resolved;
                }
            }
            catch { /* ignore and fallback to generic */ }

            if (root == null)
            {
                root = context.Resolver?.ResolveByKey(candidate.CandidateFormKey);
            }

            // If resolver didn't return anything, try direct typed resolution via concrete LinkCache
            if (root == null && context.LinkCache != null)
            {
                try
                {
                    var mfk = ToMutagenFormKey(candidate.CandidateFormKey);
                    if (mfk != null)
                    {
                        // Try common FO4 getter types in order
                        if (context.LinkCache.TryResolve<Mutagen.Bethesda.Fallout4.IObjectModificationGetter>(mfk.Value, out var omod) && omod != null)
                        {
                            root = omod;
                            _logger.LogInformation("ResolveOmod: LinkCache typed resolve SUCCESS IObjectModificationGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<Mutagen.Bethesda.Fallout4.IConstructibleObjectGetter>(mfk.Value, out var cobj) && cobj != null)
                        {
                            root = cobj;
                            _logger.LogInformation("ResolveOmod: LinkCache typed resolve SUCCESS IConstructibleObjectGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<Mutagen.Bethesda.Fallout4.IWeaponGetter>(mfk.Value, out var weap) && weap != null)
                        {
                            root = weap;
                            _logger.LogInformation("ResolveOmod: LinkCache typed resolve SUCCESS IWeaponGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<Mutagen.Bethesda.Fallout4.IAmmunitionGetter>(mfk.Value, out var ammo) && ammo != null)
                        {
                            root = ammo;
                            _logger.LogInformation("ResolveOmod: LinkCache typed resolve SUCCESS IAmmunitionGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else
                        {
                            _logger.LogInformation("ResolveOmod: LinkCache typed resolve MISS {Plugin}:{Id:X8}", candidate.CandidateFormKey.PluginName, candidate.CandidateFormKey.FormId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "ResolveOmod: direct LinkCache typed resolution failed for {Plugin}:{Id:X8}", candidate.CandidateFormKey.PluginName, candidate.CandidateFormKey.FormId);
                }
            }

            if (root == null)
            {
                _logger.LogInformation("ResolveOmod: All resolution paths returned null for {Plugin}:{Id:X8}",
                    candidate.CandidateFormKey.PluginName, candidate.CandidateFormKey.FormId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AttachPointConfirmer: failed to resolve candidate form key");
            return null;
        }
        if (root == null) { rootNull++; return null; }

        // If already looks like an OMOD (has AttachPoint), return it
        if (TryFindAttachPointLink(root) != null)
            return root;

        // If it's a COBJ, try to resolve CreatedObject -> OMOD
        try
        {
            var created = TryGetFormLinkValue(root, "CreatedObject", "CreatedObjectForm", "CreatedObjectReference");
            if (created == null) { createdObjMissing++; return null; }

            var fkProp = created.GetType().GetProperty("FormKey");
            var fkVal = fkProp?.GetValue(created);
            if (fkVal != null && TryExtractFormKeyInfo(fkVal, out var p, out var id))
            {
                _logger.LogDebug("ResolveOmod: CreatedObject link {Plugin}:{Id:X8}", p, id);
                var tempFk = new FormKey { PluginName = p, FormId = id };
                try
                {
                    var mfk2 = ToMutagenFormKey(tempFk);
                    object? omod = null;
                    if (mfk2 != null && context.Resolver != null && context.Resolver.TryResolve(mfk2.Value, out var or) && or != null)
                        omod = or;
                    else
                        omod = context.Resolver?.ResolveByKey(tempFk);

                    if (omod == null) { createdObjResolveFail++; return null; }
                    if (TryFindAttachPointLink(omod) != null) return omod;
                    createdObjNotOmod++;
                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "AttachPointConfirmer: failed to resolve CreatedObject from COBJ");
                    createdObjResolveFail++;
                    return null;
                }
            }
            else
            {
                _logger.LogDebug("ResolveOmod: Failed to extract FormKey from CreatedObject link");
                createdObjResolveFail++;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AttachPointConfirmer: failed to resolve CreatedObject from COBJ");
        }

        return null;
    }

    private Mutagen.Bethesda.Plugins.FormKey? ToMutagenFormKey(FormKey fk)
    {
        return FormKeyNormalizer.ToMutagenFormKey(fk);
    }

    private static object? TryGetFormLinkValue(object obj, params string[] propertyNames)
    {
        var type = obj.GetType();
        foreach (var name in propertyNames)
        {
            try
            {
                var prop = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (prop == null || prop.GetIndexParameters().Length > 0) continue;
                var val = prop.GetValue(obj);
                if (val == null) continue;
                // Heuristically accept if it exposes FormKey
                if (val.GetType().GetProperty("FormKey") != null)
                    return val;
            }
            catch { /* ignore and continue */ }
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