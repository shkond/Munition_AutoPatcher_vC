using System;
using System.Linq;
using System.Reflection;

namespace MunitionAutoPatcher.Utilities
{
    /// <summary>
    /// Lightweight detection helpers for Mutagen getter types without taking a direct dependency on specific interfaces.
    /// Uses a conservative, version-adaptive approach: interface name check -> signature property check -> fallback name heuristics.
    /// </summary>
    public static class MutagenTypeGuards
    {
        public static bool IsAmmoOrProjectile(object o)
        {
            if (o == null) return false;
            return IsAmmoGetter(o) || IsProjectileGetter(o);
        }

        public static bool IsAmmoGetter(object o)
        {
            if (o == null) return false;
            var t = o.GetType();
            try
            {
                // 1) Interface/type name check (e.g., IAmmoGetter)
                if (t.GetInterfaces().Any(i => i.Name.Equals("IAmmoGetter", StringComparison.Ordinal))) return true;
                if (t.Name.Equals("IAmmoGetter", StringComparison.Ordinal) || t.Name.Equals("AmmoGetter", StringComparison.Ordinal)) return true;

                // 2) Record signature check when available (e.g., Signature == "AMMO")
                var sigProp = t.GetProperty("Signature", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                              ?? t.GetProperty("RecordType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var sigVal = sigProp?.GetValue(o)?.ToString();
                if (string.Equals(sigVal, "AMMO", StringComparison.OrdinalIgnoreCase)) return true;

                // 3) Fallback: name contains
                var lname = (t.Name ?? string.Empty).ToLowerInvariant();
                if (lname.Contains("ammo")) return true;
            }
            catch { /* best-effort */ }
            return false;
        }

        public static bool IsProjectileGetter(object o)
        {
            if (o == null) return false;
            var t = o.GetType();
            try
            {
                // 1) Interface/type name check
                if (t.GetInterfaces().Any(i => i.Name.Equals("IProjectileGetter", StringComparison.Ordinal))) return true;
                if (t.Name.Equals("IProjectileGetter", StringComparison.Ordinal) || t.Name.Equals("ProjectileGetter", StringComparison.Ordinal)) return true;

                // 2) Record signature check
                var sigProp = t.GetProperty("Signature", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
                              ?? t.GetProperty("RecordType", BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                var sigVal = sigProp?.GetValue(o)?.ToString();
                if (string.Equals(sigVal, "PROJ", StringComparison.OrdinalIgnoreCase)) return true;

                // 3) Fallback: name contains
                var lname = (t.Name ?? string.Empty).ToLowerInvariant();
                if (lname.Contains("projectile")) return true;
            }
            catch { /* best-effort */ }
            return false;
        }
    }
}
