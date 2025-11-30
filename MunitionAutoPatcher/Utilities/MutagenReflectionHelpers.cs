using System;
using System.Linq;
using System.Reflection;
using MunitionAutoPatcher;
using System.Collections.Concurrent;
using Mutagen.Bethesda.Plugins;

namespace MunitionAutoPatcher.Utilities
{
    /// <summary>
    /// Collection of small reflection helpers used to extract FormKey/ModKey/FileName/ID
    /// information from adapter-provided objects. These helpers are defensive and return
    /// false on any unexpected shape or exceptions.
    /// 
    /// NOTE: This class is internal to enforce the Accessor boundary pattern.
    /// External code should use IMutagenAccessor methods instead.
    /// </summary>
    internal static class MutagenReflectionHelpers
    {
        private static readonly ConcurrentDictionary<string, int> s_msgCounts = new();
        private const int s_msgSuppressThreshold = 3;

        private static void LogOnce(string key, string message, Exception? ex = null)
        {
            try
            {
                var newCount = s_msgCounts.AddOrUpdate(key, 1, (_, old) => old + 1);
                if (newCount <= s_msgSuppressThreshold)
                {
                    AppLogger.Log(message, ex);
                }
                else if (newCount == s_msgSuppressThreshold + 1)
                {
                    AppLogger.Log($"{message} (further identical messages will be suppressed)");
                }
            }
            catch { }
        }

        public static bool TryGetFormKey(object? record, out object? formKey)
        {
            formKey = null;
            if (record == null) return false;
            try
            {
                // If the object is already a FormKey, just return it.
                if (record.GetType().FullName?.Contains("FormKey") == true)
                {
                    formKey = record;
                    return true;
                }

                var prop = record.GetType().GetProperty("FormKey");
                if (prop == null)
                {
                    return false;
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
                var t = formKey.GetType();
                // Primary: public property "ModKey"
                var prop = t.GetProperty("ModKey", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (prop != null)
                {
                    modKey = prop.GetValue(formKey);
                    if (modKey != null) return true;
                }

                // Fallback 1: public field "ModKey"
                var field = t.GetField("ModKey", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (field != null)
                {
                    modKey = field.GetValue(formKey);
                    if (modKey != null) return true;
                }

                // Fallback 2: alternative naming (some overlays might expose "Mod")
                var altProp = t.GetProperty("Mod", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (altProp != null)
                {
                    modKey = altProp.GetValue(formKey);
                    if (modKey != null) return true;
                }

                var altField = t.GetField("Mod", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (altField != null)
                {
                    modKey = altField.GetValue(formKey);
                    if (modKey != null) return true;
                }

                return false;
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
                var t = modKey.GetType();
                var prop = t.GetProperty("FileName", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                           ?? t.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (prop != null)
                {
                    var v = prop.GetValue(modKey);
                    if (v != null)
                    {
                        fileName = v.ToString() ?? string.Empty;
                        if (!string.IsNullOrEmpty(fileName) && !IsNullSentinel(fileName)) return true;
                    }
                }

                // Fallback: use ToString()
                var s = modKey.ToString();
                if (!string.IsNullOrEmpty(s) && !IsNullSentinel(s))
                {
                    fileName = s;
                    return true;
                }

                return false;
            }
            catch
            {
                fileName = string.Empty;
                return false;
            }
        }

        private static bool IsNullSentinel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return true;
            var t = s.Trim();
            return t.Equals("null", StringComparison.OrdinalIgnoreCase)
                || t.Equals("(null)", StringComparison.OrdinalIgnoreCase)
                || t.Equals("none", StringComparison.OrdinalIgnoreCase)
                || t.Equals("<null>", StringComparison.OrdinalIgnoreCase);
        }

        public static bool TryGetIdFromFormKey(object? formKey, out uint id)
        {
            id = 0u;
            if (formKey == null) return false;
            try
            {
                var t = formKey.GetType();
                var prop = t.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                        ?? t.GetProperty("FormID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                        ?? t.GetProperty("FormId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (prop != null)
                {
                    var v = prop.GetValue(formKey);
                    if (v != null)
                    {
                        if (v is uint u) { id = u; return true; }
                        try { id = Convert.ToUInt32(v); return true; } catch { /* continue */ }
                    }
                }

                // Fallback: field access
                var field = t.GetField("ID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                         ?? t.GetField("FormID", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                         ?? t.GetField("FormId", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                if (field != null)
                {
                    var v = field.GetValue(formKey);
                    if (v != null)
                    {
                        if (v is uint u2) { id = u2; return true; }
                        try { id = Convert.ToUInt32(v); return true; } catch { /* continue */ }
                    }
                }

                // Fallback: parse from ToString() like "Plugin.esp:00123456"
                if (TryParseFormKeyString(formKey, out _, out id))
                    return true;

                return false;
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
                if (record == null)
                {
                    LogOnce("mrh_record_null", "MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: record is null");
                    return false;
                }

                // Fast path for typed records
                try
                {
                    if (record is Mutagen.Bethesda.Plugins.Records.IMajorRecordGetter mrFast)
                    {
                        var fkFast = mrFast.FormKey;
                        plugin = fkFast.ModKey.FileName.ToString();
                        id = (uint)fkFast.ID;
                        return !string.IsNullOrEmpty(plugin) && id != 0u;
                    }
                }
                catch { /* fall back to reflection-based path */ }

                // Fast path for FormKey
                if (record is FormKey fkDirect)
                {
                    plugin = fkDirect.ModKey.FileName.ToString();
                    id = fkDirect.ID;
                    return !string.IsNullOrEmpty(plugin) && id != 0u;
                }

                if (!TryGetFormKey(record, out var fk))
                {
                    LogOnce($"mrh_no_formkey_{record.GetType().Name}", $"MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: failed to get FormKey from record type {record.GetType().Name}");
                    return false;
                }

                if (fk == null)
                {
                    LogOnce($"mrh_formkey_null_{record.GetType().Name}", $"MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: FormKey is null for record type {record.GetType().Name}");
                    return false;
                }

                if (!TryGetModKeyFromFormKey(fk, out var mk))
                {
                    LogOnce($"mrh_no_modkey_{fk.GetType().Name}", $"MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: failed to get ModKey from FormKey (FormKey type: {fk.GetType().Name})");
                    // Fallback: try parsing from ToString()
                    if (TryParseFormKeyString(fk, out plugin, out id))
                    {
                        return true;
                    }
                    return false;
                }

                if (mk == null)
                {
                    // ModKey itself is null, can't proceed.
                    return false;
                }

                // Check if the ModKey is logically null using the IsNull property
                try
                {
                    var isNullProp = mk.GetType().GetProperty("IsNull");
                    if (isNullProp != null && isNullProp.PropertyType == typeof(bool))
                    {
                        if ((bool)isNullProp.GetValue(mk))
                        {
                            // ModKey is logically null, so we can't get a plugin name. This is an expected case for null FormKeys.
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogOnce("mrh_isnull_check_exception", "Exception during ModKey.IsNull check", ex);
                }

                if (!TryGetFileNameFromModKey(mk, out plugin))
                {
                    LogOnce("mrh_no_filename", "MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: failed to get FileName from ModKey");
                    plugin = string.Empty;
                }

                if (!TryGetIdFromFormKey(fk, out id))
                {
                    LogOnce("mrh_no_id", "MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: failed to get ID from FormKey");
                }

                bool success = !string.IsNullOrEmpty(plugin) && id != 0u;
                if (!success)
                {
                    string fkStr = string.Empty;
                    try { fkStr = fk?.ToString() ?? string.Empty; } catch { fkStr = string.Empty; }
                    // Use a constant key to avoid per-FormKey spam; still emit details in the first few occurrences.
                    LogOnce("mrh_validation_failed", $"MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: validation failed - plugin='{plugin ?? "(null)"}', id={id:X8}, fk='{fkStr}'");
                }

                return success;
            }
            catch (Exception ex)
            {
                LogOnce("mrh_exception", "MutagenReflectionHelpers.TryGetPluginAndIdFromRecord: exception occurred", ex);
                plugin = string.Empty;
                id = 0u;
                return false;
            }
        }

        // Fallback: parse plugin and id from FormKey.ToString() like "Plugin.esp:00123456"
        private static bool TryParseFormKeyString(object formKey, out string plugin, out uint id)
        {
            plugin = string.Empty;
            id = 0u;
            try
            {
                var s = formKey?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(s)) return false;
                // Common patterns:
                //  - "Plugin.esp:00123456"
                //  - "00123456:Plugin.esp"
                var parts = s.Split(':');
                if (parts.Length == 2)
                {
                    string left = parts[0].Trim();
                    string right = parts[1].Trim();

                    // First try: right is hex id, left is plugin
                    var rightId = right.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? right.Substring(2) : right;
                    if (uint.TryParse(rightId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var idParsed)
                        && !string.IsNullOrEmpty(left))
                    {
                        plugin = left;
                        id = idParsed;
                        return true;
                    }

                    // Second try: left is hex id, right is plugin
                    var leftId = left.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? left.Substring(2) : left;
                    if (uint.TryParse(leftId, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out idParsed)
                        && !string.IsNullOrEmpty(right))
                    {
                        plugin = right;
                        id = idParsed;
                        return true;
                    }
                }
            }
            catch { /* ignore */ }
            return false;
        }

        /// <summary>
        /// Safely try to get a property value (typed) from an object via reflection.
        /// Returns true if the property exists and could be converted to T.
        /// </summary>
        public static bool TryGetPropertyValue<T>(object? obj, string propName, out T? value)
        {
            value = default;
            if (obj == null) return false;
            try
            {
                var prop = obj.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                if (prop == null) return false;
                var v = prop.GetValue(obj);
                if (v == null) return false;
                if (v is T t) { value = t; return true; }
                try { value = (T)Convert.ChangeType(v, typeof(T)); return true; } catch { return false; }
            }
            catch (Exception ex)
            {
                LogOnce($"mrh_trygetprop_{propName}", $"MutagenReflectionHelpers: TryGetPropertyValue failed for {propName}", ex);
                value = default;
                return false;
            }
        }

        /// <summary>
        /// Safely try to invoke a method (non-generic/instance) by name and return the result.
        /// Returns true if invocation succeeded.
        /// </summary>
        public static bool TryInvokeMethod(object? obj, string methodName, object?[]? args, out object? result)
        {
            result = null;
            if (obj == null) return false;
            try
            {
                var type = obj.GetType();
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                                  .Where(m => string.Equals(m.Name, methodName, StringComparison.Ordinal))
                                  .ToArray();
                if (methods.Length == 0) return false;

                // Prefer parameter-count matching
                MethodInfo? method = methods.FirstOrDefault(m => m.GetParameters().Length == (args?.Length ?? 0)) ?? methods.First();
                result = method.Invoke(obj, args);
                return true;
            }
            catch (Exception ex)
            {
                LogOnce($"mrh_tryinvokemethod_{methodName}", $"MutagenReflectionHelpers: TryInvokeMethod failed for {methodName}", ex);
                result = null;
                return false;
            }
        }
    }
}
