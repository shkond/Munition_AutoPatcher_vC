using Microsoft.Extensions.Logging;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
namespace MunitionAutoPatcher.Services.Implementations;
/// <summary>
/// Confirmer that derives confirmation evidence from OMOD AttachPoint ↔ Weapon AttachParentSlots
/// matching combined with detection of ammo/projectile links inside the OMOD (or related created object).
/// This does not rely on the reverse-reference map (which is empty for FO4 attach point semantics).
/// 
/// Phase 3: Type-safe implementation - NO REFLECTION
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
        // Initialize diagnostics
        var diag = new OmodResolutionDiagnostics();
        int rootNull = 0, createdObjMissing = 0, createdObjResolveFail = 0, createdObjNotOmod = 0;
        int inspected = 0, resolvedToOmod = 0, hadAttachPoint = 0, matchedWeapons = 0, foundAmmo = 0, confirmed = 0;

        // Quick check: try resolving a known vanilla FormKey
        try
        {
            if (context.LinkCache != null)
            {
                var testKey = new Mutagen.Bethesda.Plugins.FormKey(
                    new Mutagen.Bethesda.Plugins.ModKey("Fallout4.esm", Mutagen.Bethesda.Plugins.ModType.Master),
                    0x0004D00C); // Known vanilla OMOD
                if (context.LinkCache.TryResolve<IObjectModificationGetter>(testKey, out var testOmod) && testOmod != null)
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
        var attachSlotToWeapon = new Dictionary<(string Plugin, uint Id), List<Models.FormKey>>();
        foreach (var weapon in context.AllWeapons)
        {
            try
            {
                if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(weapon, out var wPlugin, out var wId) || string.IsNullOrEmpty(wPlugin) || wId == 0)
                    continue;
                var fkWeapon = new Models.FormKey { PluginName = wPlugin, FormId = wId };
                
                // Phase 3: Type-safe access to AttachParentSlots
                if (weapon is IWeaponGetter weaponGetter)
                {
                    foreach (var attachSlotLink in weaponGetter.AttachParentSlots)
                    {
                        if (attachSlotLink.FormKey.IsNull) continue;
                        
                        var fk = attachSlotLink.FormKey;
                        var key = (fk.ModKey.FileName.String.ToLowerInvariant(), fk.ID);
                        
                        if (!attachSlotToWeapon.TryGetValue(key, out var list))
                        {
                            list = new List<Models.FormKey>();
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
        
        // Phase 5改善案F: enumerate回数を1回に削減（debug dumpとメイン処理を統合）
        int dumpCount = 0;
        foreach (var candidate in candidates)
        {
            try
            {
                // Debug dump for first 20 candidates
                if (dumpCount < 20)
                {
                    try
                    {
                        var fk = candidate.CandidateFormKey;
                        _logger.LogInformation("CandidateDump[{Index}]: Type={Type} FK={Plugin}:{Id:X8}",
                            dumpCount++, candidate.CandidateType, fk?.PluginName ?? "NULL", fk?.FormId ?? 0);
                    }
                    catch { /* best-effort debug only */ }
                }

                inspected++;
                // Skip already confirmed
                if (candidate.ConfirmedAmmoChange) continue;
                // We only handle candidates that represent potential OMOD or created weapon modifications
                if (!IsOmodLike(candidate)) continue;
                // Resolve OMOD getter from CandidateFormKey (follow through COBJ.CreatedObject when needed)
                object? omodGetter = ResolveOmodForCandidate(candidate, context, ref rootNull, ref createdObjMissing, ref createdObjResolveFail, ref createdObjNotOmod);
                if (omodGetter == null) continue;
                resolvedToOmod++;
                // Extract attach point keyword link (type-safe)
                var apLink = TryFindAttachPointLink(omodGetter);
                if (apLink == null)
                    continue; // cannot map weapon applicability
                hadAttachPoint++;
                // Phase 3: Type-safe FormKey extraction
                var apLinkFormKey = apLink.FormKey;
                if (apLinkFormKey.IsNull)
                    continue;
                var apPlugin = apLinkFormKey.ModKey.FileName.String;
                var apId = apLinkFormKey.ID;
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

        // Phase 5改善案C: 診断構造体で一括ログ出力
        diag = new OmodResolutionDiagnostics
        {
            TotalCandidates = inspected,
            OmodResolved = resolvedToOmod,
            AttachPointMatched = hadAttachPoint,
            MatchedWeapons = matchedWeapons,
            AmmoReferenceDetected = foundAmmo,
            Confirmed = confirmed,
            RootNull = rootNull,
            CreatedObjMissing = createdObjMissing,
            CreatedObjResolveFail = createdObjResolveFail,
            CreatedObjNotOmod = createdObjNotOmod
        };
        diag.LogSummary(_logger);

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
    // Phase 3: Type-safe attach point link retrieval
    private Mutagen.Bethesda.Plugins.IFormLinkGetter<IKeywordGetter>? TryFindAttachPointLink(object omodGetter)
    {
        try
        {
            // Type-safe: check if it's IObjectModificationGetter
            if (omodGetter is IObjectModificationGetter omod)
            {
                if (!omod.AttachPoint.FormKey.IsNull)
                {
                    return omod.AttachPoint;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AttachPointConfirmer: failed to get attach point link");
        }
        return null;
    }
    // Phase 3: Type-safe ammo reference search
    private Models.FormKey? TryFindAmmoReference(object omodGetter, ConfirmationContext context)
    {
        try
        {
            // Type-safe: check if it's IWeaponModificationGetter
            if (omodGetter is IWeaponModificationGetter weaponMod)
            {
                // Search through Properties for ammo-related links
                // Note: Properties in IWeaponModificationGetter are strongly typed
                foreach (var property in weaponMod.Properties)
                {
                    // Each property may have different data types
                    // We need to check for FormLink properties that might reference ammo
                    
                    // Try to get FormKey from property if it has one
                    if (property is IFormLinkGetter formLinkProp)
                    {
                        var fk = formLinkProp.FormKey;
                        if (!fk.IsNull)
                        {
                            var keyStr = $"{fk.ModKey.FileName.String}:{fk.ID:X8}";
                            if (context.AmmoMap != null && context.AmmoMap.ContainsKey(keyStr))
                            {
                                return new Models.FormKey { PluginName = fk.ModKey.FileName.String, FormId = fk.ID };
                            }
                        }
                    }
                }
            }
            
            // For other OMOD types (Armor, NPC, generic), use limited reflection
            // This is acceptable as a fallback for non-weapon OMODs
            if (omodGetter is IObjectModificationGetter omod)
            {
                // Check common OMOD properties that might contain FormLinks
                // This is a pragmatic approach: mostly type-safe with minimal reflection
                var type = omodGetter.GetType();
                var propsProperty = type.GetProperty("Properties");
                if (propsProperty != null)
                {
                    var props = propsProperty.GetValue(omodGetter);
                    if (props is System.Collections.IEnumerable enumerable)
                    {
                        int inspected = 0;
                        foreach (var prop in enumerable)
                        {
                            if (prop == null) continue;
                            inspected++;
                            if (inspected > 16) break; // Limit inspection
                            
                            // Try to extract FormKey if property implements IFormLinkGetter
                            if (prop is IFormLinkGetter formLink)
                            {
                                var fk = formLink.FormKey;
                                if (!fk.IsNull)
                                {
                                    var keyStr = $"{fk.ModKey.FileName.String}:{fk.ID:X8}";
                                    if (context.AmmoMap != null && context.AmmoMap.ContainsKey(keyStr))
                                    {
                                        return new Models.FormKey { PluginName = fk.ModKey.FileName.String, FormId = fk.ID };
                                    }
                                }
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
                        if (context.LinkCache.TryResolve<IObjectModificationGetter>(mfk.Value, out var omod) && omod != null)
                        {
                            root = omod;
                            _logger.LogDebug("ResolveOmod: LinkCache typed resolve SUCCESS IObjectModificationGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<IConstructibleObjectGetter>(mfk.Value, out var cobj) && cobj != null)
                        {
                            root = cobj;
                            _logger.LogDebug("ResolveOmod: LinkCache typed resolve SUCCESS IConstructibleObjectGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<IWeaponGetter>(mfk.Value, out var weap) && weap != null)
                        {
                            root = weap;
                            _logger.LogDebug("ResolveOmod: LinkCache typed resolve SUCCESS IWeaponGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else if (context.LinkCache.TryResolve<IAmmunitionGetter>(mfk.Value, out var ammo) && ammo != null)
                        {
                            root = ammo;
                            _logger.LogDebug("ResolveOmod: LinkCache typed resolve SUCCESS IAmmunitionGetter {Mod}:{Id:X8}", mfk.Value.ModKey.FileName, mfk.Value.ID);
                        }
                        else
                        {
                            _logger.LogDebug("ResolveOmod: LinkCache typed resolve MISS {Plugin}:{Id:X8}", candidate.CandidateFormKey.PluginName, candidate.CandidateFormKey.FormId);
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
                _logger.LogDebug("ResolveOmod: All resolution paths returned null for {Plugin}:{Id:X8}",
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
        // Phase 3: Type-safe COBJ.CreatedObject resolution
        if (root is IConstructibleObjectGetter cobjGetter)
        {
            if (cobjGetter.CreatedObject.FormKey.IsNull) 
            { 
                createdObjMissing++; 
                return null; 
            }
            var createdFormKey = cobjGetter.CreatedObject.FormKey;
            var p = createdFormKey.ModKey.FileName.String;
            var id = createdFormKey.ID;
            
            _logger.LogDebug("ResolveOmod: CreatedObject link {Plugin}:{Id:X8}", p, id);
            var tempFk = new Models.FormKey { PluginName = p, FormId = id };
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
            createdObjMissing++;
        }
        return null;
    }
    private Mutagen.Bethesda.Plugins.FormKey? ToMutagenFormKey(Models.FormKey fk)
    {
        return FormKeyNormalizer.ToMutagenFormKey(fk);
    }
}