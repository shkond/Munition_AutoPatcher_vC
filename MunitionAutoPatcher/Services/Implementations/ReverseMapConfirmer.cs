using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Service responsible for confirming candidates through reverse-reference map analysis.
/// </summary>
public class ReverseMapConfirmer : ICandidateConfirmer
{
    private readonly IMutagenAccessor _mutagenAccessor;
    private readonly ILogger<ReverseMapConfirmer> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public ReverseMapConfirmer(
        IMutagenAccessor mutagenAccessor,
        ILogger<ReverseMapConfirmer> logger,
        ILoggerFactory loggerFactory)
    {
        _mutagenAccessor = mutagenAccessor ?? throw new ArgumentNullException(nameof(mutagenAccessor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <inheritdoc/>
    public async Task ConfirmAsync(IEnumerable<OmodCandidate> candidates, ConfirmationContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting candidate confirmation via reverse-map");

        foreach (var candidate in candidates)
        {
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                if (candidate.BaseWeapon == null)
                    continue;

                var baseKey = $"{candidate.BaseWeapon.PluginName}:{candidate.BaseWeapon.FormId:X8}";

                if (!context.ReverseMap.TryGetValue(baseKey, out var refs))
                    continue;

                foreach (var entry in refs)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        var sourceRec = entry.Record;

                        // Skip source records from excluded plugins
                        if (IsFromExcludedPlugin(sourceRec, context.ExcludedPlugins))
                            continue;

                        // Try detector first if available
                        if (TryConfirmViaDetector(candidate, sourceRec, context))
                        {
                            if (candidate.ConfirmedAmmoChange)
                                break;
                        }

                        // Inspect properties of the source record for ammo-like references
                        if (TryConfirmViaPropertyScan(candidate, sourceRec, entry, context))
                        {
                            if (candidate.ConfirmedAmmoChange)
                                break;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Error processing reverse-reference entry for candidate");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error confirming candidate");
            }
        }

        _logger.LogInformation("Candidate confirmation complete");
    }

    private bool IsFromExcludedPlugin(object record, HashSet<string> excludedPlugins)
    {
        try
        {
            if (_mutagenAccessor.TryGetPluginAndIdFromRecord(record, out var plugin, out _))
            {
                return !string.IsNullOrEmpty(plugin) && excludedPlugins.Contains(plugin);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking if record is from excluded plugin");
        }

        return false;
    }

    private bool TryConfirmViaDetector(OmodCandidate candidate, object sourceRec, ConfirmationContext context)
    {
        if (context.Detector == null)
            return false;

        try
        {
            // Get the original weapon's Ammo link
            object? originalAmmoLinkObj = null;
            try
            {
                var weaponGetter = context.AllWeapons.FirstOrDefault(w =>
                {
                    try
                    {
                        if (_mutagenAccessor.TryGetPluginAndIdFromRecord(w, out var wPlugin, out var wId))
                        {
                            return string.Equals(wPlugin, candidate.BaseWeapon!.PluginName, StringComparison.OrdinalIgnoreCase)
                                   && wId == candidate.BaseWeapon.FormId;
                        }
                    }
                    catch { }
                    return false;
                });

                if (weaponGetter != null)
                {
                    originalAmmoLinkObj = weaponGetter.GetType().GetProperty("Ammo")?.GetValue(weaponGetter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to obtain original ammo link");
            }

            // Check if detector reports an ammo change
            if (context.Detector.DoesOmodChangeAmmo(sourceRec, originalAmmoLinkObj, out var newAmmoLinkObj))
            {
                try
                {
                    var fk = newAmmoLinkObj?.GetType().GetProperty("FormKey")?.GetValue(newAmmoLinkObj);
                    if (fk != null)
                    {
                        var mk = fk.GetType().GetProperty("ModKey")?.GetValue(fk);
                        var idObj = fk.GetType().GetProperty("ID")?.GetValue(fk);
                        var plugin = mk?.GetType().GetProperty("FileName")?.GetValue(mk)?.ToString() ?? string.Empty;

                        uint id = 0;
                        if (idObj is uint ui)
                            id = ui;
                        else if (idObj != null)
                            id = Convert.ToUInt32(idObj);

                        if (!string.IsNullOrEmpty(plugin) && id != 0)
                        {
                            candidate.ConfirmedAmmoChange = true;
                            candidate.CandidateAmmo = new FormKey { PluginName = plugin, FormId = id };
                            candidate.CandidateAmmoName = _mutagenAccessor.GetEditorId(newAmmoLinkObj);
                            candidate.ConfirmReason = $"Detector {context.Detector.Name} reported change";
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to process detector result");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Detector invocation failed");
        }

        return false;
    }

    private bool TryConfirmViaPropertyScan(OmodCandidate candidate, object sourceRec,
        (object Record, string PropName, object PropValue) entry, ConfirmationContext context)
    {
        try
        {
            var props = sourceRec.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in props)
            {
                context.CancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var propValue = prop.GetValue(sourceRec);
                    if (propValue == null)
                        continue;

                    // Try to extract FormKey info
                    if (!_mutagenAccessor.TryGetPluginAndIdFromRecord(propValue, out var plugin, out var id))
                        continue;

                    // Try to resolve the FormKey
                    object? resolved = null;
                    if (context.Resolver != null)
                    {
                        try
                        {
                            context.Resolver.TryResolve(propValue, out resolved);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Resolver.TryResolve failed");
                        }
                    }
                    else if (context.LinkCache != null)
                    {
                        try
                        {
                            var tmpResolver = new LinkResolver(context.LinkCache, _loggerFactory.CreateLogger<LinkResolver>());
                            tmpResolver.TryResolve(propValue, out resolved);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "LinkCache resolution failed");
                        }
                    }

                    // Check if this is ammo
                    bool isAmmo = false;
                    string resolvedTypeName = string.Empty;
                    object? resolvedGetter = null;

                    if (resolved != null)
                    {
                        resolvedGetter = resolved;
                        resolvedTypeName = resolved.GetType().Name;
                        var lname = resolvedTypeName.ToLowerInvariant();
                        if (lname.Contains("ammo") || lname.Contains("projectile") || lname.Contains("projectilegetter"))
                        {
                            isAmmo = true;
                        }
                    }

                    // Fallback: check ammo map
                    if (!isAmmo && context.AmmoMap != null)
                    {
                        var formKeyStr = $"{plugin}:{id:X8}";
                        if (context.AmmoMap.TryGetValue(formKeyStr, out var ammoGetterObj))
                        {
                            isAmmo = true;
                            resolvedGetter = ammoGetterObj;
                            resolvedTypeName = ammoGetterObj.GetType().Name;
                        }
                    }

                    if (isAmmo)
                    {
                        // Confirm candidate
                        candidate.ConfirmedAmmoChange = true;
                        candidate.ConfirmReason = $"Resolved {prop.Name} -> {resolvedTypeName} on {entry.Record.GetType().Name}";
                        candidate.CandidateAmmo = new FormKey { PluginName = plugin, FormId = id };

                        if (resolvedGetter != null)
                        {
                            candidate.CandidateAmmoName = _mutagenAccessor.GetEditorId(resolvedGetter);
                        }

                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error scanning property {PropName} for ammo references", prop.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error scanning source record properties");
        }

        return false;
    }
}
