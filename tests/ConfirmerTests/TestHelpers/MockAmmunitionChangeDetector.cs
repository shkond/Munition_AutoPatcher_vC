// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using MunitionAutoPatcher.Services.Interfaces;

namespace ConfirmerTests.TestHelpers;

/// <summary>
/// Mock implementation of IAmmunitionChangeDetector for testing.
/// Allows configuring which OMODs should report ammo changes.
/// </summary>
public sealed class MockAmmunitionChangeDetector : IAmmunitionChangeDetector
{
    private readonly Dictionary<object, (bool Changes, object? NewAmmoLink)> _configuredResponses = new();
    private bool _defaultResponse = false;
    private object? _defaultNewAmmoLink = null;

    public string Name => "MockDetector";

    /// <summary>
    /// Configures the detector to return a specific response for a given OMOD.
    /// </summary>
    public MockAmmunitionChangeDetector WithResponse(object omod, bool changesAmmo, object? newAmmoLink = null)
    {
        _configuredResponses[omod] = (changesAmmo, newAmmoLink);
        return this;
    }

    /// <summary>
    /// Sets the default response when no specific configuration exists.
    /// </summary>
    public MockAmmunitionChangeDetector WithDefaultResponse(bool changesAmmo, object? newAmmoLink = null)
    {
        _defaultResponse = changesAmmo;
        _defaultNewAmmoLink = newAmmoLink;
        return this;
    }

    public bool DoesOmodChangeAmmo(object omod, object? originalAmmoLink, out object? newAmmoLink)
    {
        if (_configuredResponses.TryGetValue(omod, out var response))
        {
            newAmmoLink = response.NewAmmoLink;
            return response.Changes;
        }

        newAmmoLink = _defaultNewAmmoLink;
        return _defaultResponse;
    }
}
