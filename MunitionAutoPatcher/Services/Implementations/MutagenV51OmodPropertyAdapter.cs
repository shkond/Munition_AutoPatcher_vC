using Microsoft.Extensions.Logging;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using MunitionAutoPatcher.Services.Interfaces;

namespace MunitionAutoPatcher.Services.Implementations;

/// <summary>
/// Extracts ammo FormKey data from FO4 weapon OMOD properties without using reflection.
/// </summary>
public sealed class MutagenV51OmodPropertyAdapter : IOmodPropertyAdapter
{
    private readonly ILogger<MutagenV51OmodPropertyAdapter> _logger;

    public MutagenV51OmodPropertyAdapter(ILogger<MutagenV51OmodPropertyAdapter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool TryExtractFormKeyFromAmmoProperty(
        IAObjectModPropertyGetter<Weapon.Property> prop,
        IWeaponModificationGetter weaponMod,
        out FormKey formKey)
    {
        if (prop == null) throw new ArgumentNullException(nameof(prop));
        if (weaponMod == null) throw new ArgumentNullException(nameof(weaponMod));

        formKey = default;

        if (prop is not IObjectModFormLinkIntPropertyGetter<Weapon.Property> formLinkProp)
        {
            _logger.LogDebug("Property type {PropType} is not FormLinkInt for weapon OMOD {EditorId}",
                prop.GetType().Name, weaponMod.EditorID);
            return false;
        }

        var recordFormKey = formLinkProp.Record?.FormKeyNullable;
        if (recordFormKey.HasValue && !recordFormKey.Value.IsNull)
        {
            formKey = recordFormKey.Value;
            return true;
        }

        var rawValue = formLinkProp.Value;
        if (rawValue == 0)
        {
            _logger.LogDebug("FormLinkInt property Value was 0 for weapon OMOD {EditorId}", weaponMod.EditorID);
            return false;
        }

        var modKey = weaponMod.FormKey.ModKey;
        if (modKey.IsNull)
        {
            throw new InvalidOperationException("Weapon OMOD ModKey is null while property Value is non-zero.");
        }

        formKey = new FormKey(modKey, rawValue);
        return true;
    }
}
