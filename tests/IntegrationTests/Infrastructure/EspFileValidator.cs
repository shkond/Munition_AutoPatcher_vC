// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure.Models;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T011: Validates generated ESP files using Mutagen's binary overlay for structural checks
/// and header normalization for deterministic comparisons.
/// 
/// Per research.md:
/// - Uses Fallout4Mod.CreateFromBinaryOverlay for read-only structural counts
/// - Classifies errors as Fatal (blocks CI) vs Warning (accumulated but non-blocking)
/// - Normalizes volatile header fields (timestamp, NextFormId) before byte-level diffs
/// </summary>
public sealed class EspFileValidator
{
    private const int SmallFileSizeThreshold = 1024; // 1KB - files smaller trigger warning

    /// <summary>
    /// Validates an ESP file against the given validation profile.
    /// </summary>
    /// <param name="espPath">Absolute path to the ESP file to validate.</param>
    /// <param name="profile">Validation profile specifying expected counts and checks.</param>
    /// <returns>Validation result containing counts, errors, and warnings.</returns>
    public ValidationResult Validate(string espPath, ESPValidationProfile profile)
    {
        var result = new ValidationResult();

        // Phase 1: File existence check (Fatal if missing)
        if (!File.Exists(espPath))
        {
            result.AddError($"ESP file not found: {espPath}");
            return result;
        }

        // Phase 2: File size check
        var fileInfo = new FileInfo(espPath);
        result.FileSizeBytes = fileInfo.Length;

        if (fileInfo.Length < SmallFileSizeThreshold)
        {
            var warningMsg = $"Small file size ({fileInfo.Length} bytes < {SmallFileSizeThreshold} bytes)";
            if (profile.AllowedWarnings?.Any(w => warningMsg.Contains(w, StringComparison.OrdinalIgnoreCase)) == true)
            {
                result.AddWarning(warningMsg);
            }
            else
            {
                result.AddWarning(warningMsg);
            }
        }

        // Phase 3: Structural validation via Mutagen overlay
        try
        {
            var modKey = ModKey.FromFileName(Path.GetFileName(espPath));
            var modPath = new ModPath(modKey, espPath);

            using var mod = Fallout4Mod.CreateFromBinaryOverlay(modPath, Fallout4Release.Fallout4);

            // Count WEAP records
            result.WeaponCount = mod.Weapons.Count;

            // Count AMMO records
            result.AmmoCount = mod.Ammunitions.Count;

            // Count COBJ records
            result.CobjCount = mod.ConstructibleObjects.Count;

            // Total record count
            result.RecordCount = result.WeaponCount + result.AmmoCount + result.CobjCount;

            // Header validation
            result.HasValidHeader = true;

            // Validate structural expectations
            ValidateStructuralExpectations(result, profile.StructuralExpectations);

            // Execute custom checks
            if (profile.StructuralExpectations?.CustomChecks != null)
            {
                foreach (var check in profile.StructuralExpectations.CustomChecks)
                {
                    var checkResult = check.Execute(mod);
                    if (!checkResult.Passed)
                    {
                        result.AddError($"Custom check '{check.Description}' failed: {checkResult.ErrorMessage}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Fatal: Mutagen overlay parse failure indicates corrupted ESP
            result.AddError($"Failed to parse ESP via Mutagen overlay: {ex.Message}");
            result.HasValidHeader = false;
        }

        return result;
    }

    /// <summary>
    /// Validates structural expectations against the parsed counts.
    /// </summary>
    private void ValidateStructuralExpectations(ValidationResult result, StructuralExpectation? expectations)
    {
        if (expectations == null) return;

        if (expectations.WeaponCount != null && !expectations.WeaponCount.Value.Contains(result.WeaponCount))
        {
            result.AddError($"Weapon count {result.WeaponCount} outside expected range {expectations.WeaponCount}");
        }

        if (expectations.AmmoCount != null && !expectations.AmmoCount.Value.Contains(result.AmmoCount))
        {
            result.AddError($"Ammo count {result.AmmoCount} outside expected range {expectations.AmmoCount}");
        }

        if (expectations.CobjCount != null && !expectations.CobjCount.Value.Contains(result.CobjCount))
        {
            result.AddError($"COBJ count {result.CobjCount} outside expected range {expectations.CobjCount}");
        }
    }

    /// <summary>
    /// Normalizes header fields in an ESP file to enable deterministic byte-level comparison.
    /// Creates a copy of the file with volatile fields zeroed out.
    /// </summary>
    /// <param name="sourcePath">Path to the source ESP file.</param>
    /// <param name="destPath">Path where the normalized ESP will be written.</param>
    /// <param name="fieldsToNormalize">Header fields to zero out.</param>
    /// <returns>True if normalization succeeded; false otherwise.</returns>
    public bool NormalizeHeader(string sourcePath, string destPath, IEnumerable<HeaderField> fieldsToNormalize)
    {
        if (!File.Exists(sourcePath))
            return false;

        try
        {
            // Read the entire file into memory
            var bytes = File.ReadAllBytes(sourcePath);

            // ESP Header structure (TES4 record):
            // Offset 0-3: Record type "TES4" (4 bytes)
            // Offset 4-7: Data size (4 bytes)
            // Offset 8-11: Flags (4 bytes)
            // Offset 12-15: FormID (4 bytes)
            // Offset 16-19: Timestamp (4 bytes) - normalize this
            // Offset 20-23: Version control info (4 bytes)
            // Offset 24-25: Internal version (2 bytes)
            // Offset 26-27: Unknown (2 bytes)

            // Verify this is an ESP/ESM file (starts with TES4)
            if (bytes.Length < 28 || bytes[0] != 'T' || bytes[1] != 'E' || bytes[2] != 'S' || bytes[3] != '4')
            {
                return false;
            }

            foreach (var field in fieldsToNormalize)
            {
                switch (field)
                {
                    case HeaderField.Timestamp:
                        // Zero out timestamp at offset 16-19
                        if (bytes.Length >= 20)
                        {
                            bytes[16] = 0;
                            bytes[17] = 0;
                            bytes[18] = 0;
                            bytes[19] = 0;
                        }
                        break;

                    case HeaderField.NextFormId:
                        // NextFormId is in the HEDR subrecord within TES4
                        // We need to find and modify it
                        NormalizeNextFormId(bytes);
                        break;

                    case HeaderField.Author:
                        // Author is in CNAM subrecord - more complex, skip for now
                        break;

                    case HeaderField.Description:
                        // Description is in SNAM subrecord - more complex, skip for now
                        break;
                }
            }

            // Write the normalized file
            File.WriteAllBytes(destPath, bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Zeros out the NextFormId field in the HEDR subrecord.
    /// </summary>
    private void NormalizeNextFormId(byte[] bytes)
    {
        // HEDR subrecord is typically the first subrecord in TES4
        // Structure: HEDR + size(2) + version(4) + numRecords(4) + nextObjectId(4)

        // Find HEDR within the TES4 record
        var dataStart = 24; // After TES4 header
        var maxSearch = Math.Min(bytes.Length, dataStart + 100); // Limit search

        for (int i = dataStart; i < maxSearch - 4; i++)
        {
            if (bytes[i] == 'H' && bytes[i + 1] == 'E' && bytes[i + 2] == 'D' && bytes[i + 3] == 'R')
            {
                // Found HEDR, now find NextFormId
                // HEDR: type(4) + size(2) + version(4) + numRecords(4) + nextObjectId(4)
                var nextFormIdOffset = i + 4 + 2 + 4 + 4;
                if (nextFormIdOffset + 4 <= bytes.Length)
                {
                    bytes[nextFormIdOffset] = 0;
                    bytes[nextFormIdOffset + 1] = 0;
                    bytes[nextFormIdOffset + 2] = 0;
                    bytes[nextFormIdOffset + 3] = 0;
                }
                break;
            }
        }
    }

    /// <summary>
    /// Compares two ESP files after normalizing headers.
    /// </summary>
    /// <param name="espPath">Path to the generated ESP.</param>
    /// <param name="baselinePath">Path to the baseline ESP.</param>
    /// <param name="ignoreFields">Header fields to ignore during comparison.</param>
    /// <returns>Comparison result with any differences found.</returns>
    public ComparisonResult CompareWithBaseline(string espPath, string baselinePath, IEnumerable<HeaderField> ignoreFields)
    {
        var result = new ComparisonResult();

        if (!File.Exists(espPath))
        {
            result.AddDifference("Generated ESP not found");
            return result;
        }

        if (!File.Exists(baselinePath))
        {
            result.AddDifference("Baseline ESP not found");
            return result;
        }

        try
        {
            // Create temp files for normalized comparison
            var tempDir = Path.Combine(Path.GetTempPath(), "EspFileValidator", Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var normalizedEsp = Path.Combine(tempDir, "generated.esp");
                var normalizedBaseline = Path.Combine(tempDir, "baseline.esp");

                var fieldsToNormalize = ignoreFields.ToList();
                if (!fieldsToNormalize.Any())
                {
                    fieldsToNormalize = [HeaderField.Timestamp, HeaderField.NextFormId];
                }

                if (!NormalizeHeader(espPath, normalizedEsp, fieldsToNormalize))
                {
                    result.AddDifference("Failed to normalize generated ESP header");
                    return result;
                }

                if (!NormalizeHeader(baselinePath, normalizedBaseline, fieldsToNormalize))
                {
                    result.AddDifference("Failed to normalize baseline ESP header");
                    return result;
                }

                // Compare byte-by-byte
                var espBytes = File.ReadAllBytes(normalizedEsp);
                var baselineBytes = File.ReadAllBytes(normalizedBaseline);

                if (espBytes.Length != baselineBytes.Length)
                {
                    result.AddDifference($"File size mismatch: generated={espBytes.Length}, baseline={baselineBytes.Length}");
                }
                else
                {
                    for (int i = 0; i < espBytes.Length; i++)
                    {
                        if (espBytes[i] != baselineBytes[i])
                        {
                            result.AddDifference($"Byte difference at offset 0x{i:X}: generated=0x{espBytes[i]:X2}, baseline=0x{baselineBytes[i]:X2}");
                            if (result.Differences.Count >= 10)
                            {
                                result.AddDifference("(additional differences truncated)");
                                break;
                            }
                        }
                    }
                }

                result.AreIdentical = result.Differences.Count == 0;
            }
            finally
            {
                // Cleanup temp directory
                try { Directory.Delete(tempDir, recursive: true); } catch { }
            }
        }
        catch (Exception ex)
        {
            result.AddDifference($"Comparison failed: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Gets structural counts from an ESP without full validation.
    /// Useful for quick diagnostics.
    /// </summary>
    /// <param name="espPath">Path to the ESP file.</param>
    /// <returns>Tuple of (weaponCount, ammoCount, cobjCount) or null if parsing fails.</returns>
    public (int Weapons, int Ammo, int Cobj)? GetCounts(string espPath)
    {
        if (!File.Exists(espPath))
            return null;

        try
        {
            var modKey = ModKey.FromFileName(Path.GetFileName(espPath));
            var modPath = new ModPath(modKey, espPath);

            using var mod = Fallout4Mod.CreateFromBinaryOverlay(modPath, Fallout4Release.Fallout4);

            return (mod.Weapons.Count, mod.Ammunitions.Count, mod.ConstructibleObjects.Count);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Result of comparing two ESP files.
/// </summary>
public sealed class ComparisonResult
{
    /// <summary>
    /// Whether the files are byte-identical after normalization.
    /// </summary>
    public bool AreIdentical { get; set; }

    /// <summary>
    /// List of differences found.
    /// </summary>
    public List<string> Differences { get; init; } = [];

    /// <summary>
    /// Adds a difference description.
    /// </summary>
    public void AddDifference(string diff)
    {
        if (!string.IsNullOrWhiteSpace(diff))
            Differences.Add(diff);
    }
}
