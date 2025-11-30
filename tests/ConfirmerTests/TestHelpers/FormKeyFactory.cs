// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using Mutagen.Bethesda.Plugins;

namespace ConfirmerTests.TestHelpers;

/// <summary>
/// Factory for creating consistent FormKey instances in tests.
/// </summary>
public static class FormKeyFactory
{
    private static uint _nextId = 0x800; // Start after reserved form IDs

    /// <summary>
    /// Creates a Mutagen FormKey from plugin name and ID.
    /// </summary>
    public static FormKey Create(string pluginName, uint formId)
    {
        var modKey = ModKey.FromFileName(pluginName);
        return new FormKey(modKey, formId);
    }

    /// <summary>
    /// Creates a Model FormKey from plugin name and ID.
    /// </summary>
    public static MunitionAutoPatcher.Models.FormKey CreateModel(string pluginName, uint formId)
    {
        return new MunitionAutoPatcher.Models.FormKey
        {
            PluginName = pluginName,
            FormId = formId
        };
    }

    /// <summary>
    /// Creates a unique FormKey for a given plugin with auto-incrementing ID.
    /// Useful for generating test data without ID conflicts.
    /// </summary>
    public static FormKey CreateUnique(string pluginName)
    {
        var modKey = ModKey.FromFileName(pluginName);
        return new FormKey(modKey, Interlocked.Increment(ref _nextId));
    }

    /// <summary>
    /// Creates a unique Model FormKey for a given plugin with auto-incrementing ID.
    /// </summary>
    public static MunitionAutoPatcher.Models.FormKey CreateUniqueModel(string pluginName)
    {
        return new MunitionAutoPatcher.Models.FormKey
        {
            PluginName = pluginName,
            FormId = Interlocked.Increment(ref _nextId)
        };
    }

    /// <summary>
    /// Converts a FormKey to the standard string format "Plugin.esp:XXXXXXXX".
    /// </summary>
    public static string ToKeyString(FormKey formKey)
    {
        return $"{formKey.ModKey.FileName}:{formKey.ID:X8}";
    }

    /// <summary>
    /// Converts a Model FormKey to the standard string format "Plugin.esp:XXXXXXXX".
    /// </summary>
    public static string ToKeyString(MunitionAutoPatcher.Models.FormKey formKey)
    {
        return $"{formKey.PluginName}:{formKey.FormId:X8}";
    }

    /// <summary>
    /// Resets the ID counter. Call between test classes if needed.
    /// </summary>
    public static void ResetIdCounter()
    {
        _nextId = 0x800;
    }
}
