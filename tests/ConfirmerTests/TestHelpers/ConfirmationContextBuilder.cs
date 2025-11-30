// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Interfaces;

namespace ConfirmerTests.TestHelpers;

/// <summary>
/// Builder for creating ConfirmationContext instances for testing.
/// Provides fluent API for configuring test scenarios.
/// </summary>
public sealed class ConfirmationContextBuilder
{
    private readonly Dictionary<string, List<(object Record, string PropName, object PropValue)>> _reverseMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedPlugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<object> _allWeapons = new();
    private readonly Dictionary<string, object> _ammoMap = new(StringComparer.OrdinalIgnoreCase);
    private IAmmunitionChangeDetector? _detector;
    private ILinkResolver? _resolver;
    private Mutagen.Bethesda.Plugins.Cache.ILinkCache? _linkCache;
    private CancellationToken _cancellationToken = CancellationToken.None;

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    public static ConfirmationContextBuilder Create() => new();

    /// <summary>
    /// Adds an entry to the reverse map.
    /// </summary>
    /// <param name="formKeyString">FormKey string in format "Plugin.esp:XXXXXXXX"</param>
    /// <param name="record">The referencing record object</param>
    /// <param name="propName">Property name that contains the reference</param>
    /// <param name="propValue">Property value (the reference itself)</param>
    public ConfirmationContextBuilder WithReverseMapEntry(string formKeyString, object record, string propName, object propValue)
    {
        if (!_reverseMap.TryGetValue(formKeyString, out var list))
        {
            list = new List<(object, string, object)>();
            _reverseMap[formKeyString] = list;
        }
        list.Add((record, propName, propValue));
        return this;
    }

    /// <summary>
    /// Adds a plugin to the excluded list.
    /// </summary>
    public ConfirmationContextBuilder WithExcludedPlugin(string pluginName)
    {
        _excludedPlugins.Add(pluginName);
        return this;
    }

    /// <summary>
    /// Adds a weapon record to the all weapons list.
    /// </summary>
    public ConfirmationContextBuilder WithWeapon(object weapon)
    {
        _allWeapons.Add(weapon);
        return this;
    }

    /// <summary>
    /// Adds an ammo record to the ammo map.
    /// </summary>
    /// <param name="formKeyString">FormKey string in format "Plugin.esp:XXXXXXXX"</param>
    /// <param name="ammoRecord">The ammunition record object</param>
    public ConfirmationContextBuilder WithAmmo(string formKeyString, object ammoRecord)
    {
        _ammoMap[formKeyString] = ammoRecord;
        return this;
    }

    /// <summary>
    /// Sets the ammunition change detector.
    /// </summary>
    public ConfirmationContextBuilder WithDetector(IAmmunitionChangeDetector detector)
    {
        _detector = detector;
        return this;
    }

    /// <summary>
    /// Sets the link resolver.
    /// </summary>
    public ConfirmationContextBuilder WithResolver(ILinkResolver resolver)
    {
        _resolver = resolver;
        return this;
    }

    /// <summary>
    /// Sets the Mutagen link cache.
    /// </summary>
    public ConfirmationContextBuilder WithLinkCache(Mutagen.Bethesda.Plugins.Cache.ILinkCache linkCache)
    {
        _linkCache = linkCache;
        return this;
    }

    /// <summary>
    /// Sets the cancellation token.
    /// </summary>
    public ConfirmationContextBuilder WithCancellationToken(CancellationToken token)
    {
        _cancellationToken = token;
        return this;
    }

    /// <summary>
    /// Builds the ConfirmationContext with configured options.
    /// </summary>
    public ConfirmationContext Build()
    {
        return new ConfirmationContext
        {
            ReverseMap = _reverseMap,
            ExcludedPlugins = _excludedPlugins,
            AllWeapons = _allWeapons,
            AmmoMap = _ammoMap.Count > 0 ? _ammoMap : null,
            Detector = _detector,
            Resolver = _resolver,
            LinkCache = _linkCache,
            CancellationToken = _cancellationToken
        };
    }
}
