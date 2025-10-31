using System;

namespace MunitionAutoPatcher.Utilities
{
    /// <summary>
    /// Collection of small reflection helpers used to extract FormKey/ModKey/FileName/ID
    /// information from adapter-provided objects. These helpers are defensive and return
    /// false on any unexpected shape or exceptions.
    /// </summary>
    public static class MutagenReflectionHelpers
    {
        public static bool TryGetFormKey(object? record, out object? formKey)
        {
            formKey = null;
            if (record == null) return false;
            try
            {
                var prop = record.GetType().GetProperty("FormKey");
                if (prop == null)
                {
                    formKey = record;
                    return true;
                }
                formKey = prop.GetValue(record);
                return formKey != null;
            }
            catch
            {
                formKey = null;
                return false;
            }
        }

        public static bool TryGetModKeyFromFormKey(object? formKey, out object? modKey)
        {
            modKey = null;
            if (formKey == null) return false;
            try
            {
                var prop = formKey.GetType().GetProperty("ModKey");
                if (prop == null) return false;
                modKey = prop.GetValue(formKey);
                return modKey != null;
            }
            catch
            {
                modKey = null;
                return false;
            }
        }

        public static bool TryGetFileNameFromModKey(object? modKey, out string fileName)
        {
            fileName = string.Empty;
            if (modKey == null) return false;
            try
            {
                var prop = modKey.GetType().GetProperty("FileName");
                if (prop == null) return false;
                var v = prop.GetValue(modKey);
                if (v == null) return false;
                fileName = v.ToString() ?? string.Empty;
                return !string.IsNullOrEmpty(fileName);
            }
            catch
            {
                fileName = string.Empty;
                return false;
            }
        }

        public static bool TryGetIdFromFormKey(object? formKey, out uint id)
        {
            id = 0u;
            if (formKey == null) return false;
            try
            {
                var prop = formKey.GetType().GetProperty("ID");
                if (prop == null) return false;
                var v = prop.GetValue(formKey);
                if (v == null) return false;
                if (v is uint u) { id = u; return true; }
                try { id = Convert.ToUInt32(v); return true; } catch { return false; }
            }
            catch
            {
                id = 0u;
                return false;
            }
        }

        /// <summary>
        /// Try to obtain plugin filename and FormId (uint) from a record or a FormKey-like object.
        /// Returns true if both plugin and id were obtained.
        /// </summary>
        public static bool TryGetPluginAndIdFromRecord(object? record, out string plugin, out uint id)
        {
            plugin = string.Empty;
            id = 0u;
            try
            {
                if (record == null) return false;
                if (!TryGetFormKey(record, out var fk)) return false;
                if (fk == null) return false;
                if (!TryGetModKeyFromFormKey(fk, out var mk)) return false;
                if (!TryGetFileNameFromModKey(mk, out plugin)) plugin = string.Empty;
                TryGetIdFromFormKey(fk, out id);
                return !string.IsNullOrEmpty(plugin) && id != 0u;
            }
            catch
            {
                plugin = string.Empty;
                id = 0u;
                return false;
            }
        }
    }
}
