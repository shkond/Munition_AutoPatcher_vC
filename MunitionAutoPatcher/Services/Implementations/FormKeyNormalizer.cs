using System;
using Mutagen.Bethesda.Plugins;

namespace MunitionAutoPatcher.Services.Implementations
{
    /// <summary>
    /// Centralized FormKey normalization and conversion utility.
    /// </summary>
    public static class FormKeyNormalizer
    {
        /// <summary>
        /// Converts custom FormKey to Mutagen FormKey with proper ModType detection.
        /// </summary>
        public static FormKey? ToMutagenFormKey(Models.FormKey fk)
        {
            if (fk == null || string.IsNullOrWhiteSpace(fk.PluginName) || fk.FormId == 0)
                return null;

            try
            {
                var fileName = System.IO.Path.GetFileName(fk.PluginName);
                if (string.IsNullOrWhiteSpace(fileName))
                    fileName = fk.PluginName;

                var modType = fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) ? ModType.Master
                           : fileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase) ? ModType.Light
                           : ModType.Plugin;

                var modKey = new ModKey(fileName, modType);
                return new FormKey(modKey, fk.FormId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Normalizes plugin name (ensures proper extension and casing).
        /// </summary>
        public static string NormalizePluginName(string plugin)
        {
            if (string.IsNullOrWhiteSpace(plugin))
                return string.Empty;

            var fileName = System.IO.Path.GetFileName(plugin);
            if (string.IsNullOrWhiteSpace(fileName))
                return plugin;

            // Ensure extension exists
            if (!fileName.EndsWith(".esp", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".esp";
            }

            return fileName;
        }
    }
}
