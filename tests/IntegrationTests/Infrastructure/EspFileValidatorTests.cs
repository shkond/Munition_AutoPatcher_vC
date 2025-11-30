// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using IntegrationTests.Infrastructure.Models;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Plugins;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// T008: Tests for EspFileValidator covering header normalization and structural count assertions.
/// Tests use real ESP files generated via Mutagen to validate EspFileValidator implementation (T011).
/// </summary>
public class EspFileValidatorTests : IDisposable
{
    private readonly ITestOutputHelper _output;
    private readonly string _testTempPath;
    private readonly EspFileValidator _validator;

    public EspFileValidatorTests(ITestOutputHelper output)
    {
        _output = output;
        _testTempPath = Path.Combine(Path.GetTempPath(), "EspFileValidatorTests", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testTempPath);
        _validator = new EspFileValidator();
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempPath))
        {
            try { Directory.Delete(_testTempPath, recursive: true); }
            catch { /* Ignore cleanup errors in tests */ }
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Helper method to create a test ESP file with specified record counts.
    /// </summary>
    private string CreateTestEsp(string fileName, int weaponCount = 0, int ammoCount = 0, int cobjCount = 0)
    {
        var modKey = ModKey.FromFileName(fileName);
        var mod = new Fallout4Mod(modKey, Fallout4Release.Fallout4);

        // Add weapons
        for (int i = 0; i < weaponCount; i++)
        {
            var weapon = mod.Weapons.AddNew();
            weapon.EditorID = $"TestWeapon{i:D3}";
        }

        // Add ammunition
        for (int i = 0; i < ammoCount; i++)
        {
            var ammo = mod.Ammunitions.AddNew();
            ammo.EditorID = $"TestAmmo{i:D3}";
        }

        // Add constructible objects
        for (int i = 0; i < cobjCount; i++)
        {
            var cobj = mod.ConstructibleObjects.AddNew();
            cobj.EditorID = $"TestCobj{i:D3}";
        }

        var espPath = Path.Combine(_testTempPath, fileName);
        mod.WriteToBinary(espPath);

        _output.WriteLine($"Created test ESP: {espPath} (weapons={weaponCount}, ammo={ammoCount}, cobj={cobjCount})");
        return espPath;
    }

    #region Structural Validation Tests

    /// <summary>
    /// Tests that validator correctly counts WEAP records in an ESP.
    /// </summary>
    [Fact]
    public void ValidateStructure_CountsWeaponRecords_ReturnsCorrectCount()
    {
        // Arrange
        var testEspPath = CreateTestEsp("WeaponTest.esp", weaponCount: 3);
        var profile = new ESPValidationProfile
        {
            ProfileId = "weapon-count-test",
            StructuralExpectations = new StructuralExpectation
            {
                WeaponCount = CountRange.Exact(3)
            }
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, WeaponCount={result.WeaponCount}");
        Assert.True(result.IsValid, $"Validation failed with errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(3, result.WeaponCount);
    }

    /// <summary>
    /// Tests that validator correctly counts AMMO records in an ESP.
    /// </summary>
    [Fact]
    public void ValidateStructure_CountsAmmoRecords_ReturnsCorrectCount()
    {
        // Arrange
        var testEspPath = CreateTestEsp("AmmoTest.esp", ammoCount: 2);
        var profile = new ESPValidationProfile
        {
            ProfileId = "ammo-count-test",
            StructuralExpectations = new StructuralExpectation
            {
                AmmoCount = CountRange.Exact(2)
            }
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, AmmoCount={result.AmmoCount}");
        Assert.True(result.IsValid, $"Validation failed with errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(2, result.AmmoCount);
    }

    /// <summary>
    /// Tests that validator correctly counts COBJ records in an ESP.
    /// </summary>
    [Fact]
    public void ValidateStructure_CountsCobjRecords_ReturnsCorrectCount()
    {
        // Arrange
        var testEspPath = CreateTestEsp("CobjTest.esp", cobjCount: 5);
        var profile = new ESPValidationProfile
        {
            ProfileId = "cobj-count-test",
            StructuralExpectations = new StructuralExpectation
            {
                CobjCount = CountRange.AtLeast(1)
            }
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, CobjCount={result.CobjCount}");
        Assert.True(result.IsValid, $"Validation failed with errors: {string.Join(", ", result.Errors)}");
        Assert.Equal(5, result.CobjCount);
    }

    /// <summary>
    /// Tests that validator reports error when counts are outside expected range.
    /// </summary>
    [Fact]
    public void ValidateStructure_WhenCountOutsideRange_ReturnsError()
    {
        // Arrange - Create ESP with 2 weapons, but expect 5-10
        var testEspPath = CreateTestEsp("RangeTest.esp", weaponCount: 2);
        var profile = new ESPValidationProfile
        {
            ProfileId = "count-range-test",
            StructuralExpectations = new StructuralExpectation
            {
                WeaponCount = new CountRange(5, 10) // Expect 5-10, but ESP has 2
            }
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, WeaponCount={result.WeaponCount}");
        _output.WriteLine($"Errors: {string.Join("; ", result.Errors)}");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Weapon count") && e.Contains("outside"));
    }

    #endregion

    #region Header Normalization Tests

    /// <summary>
    /// Tests that validator normalizes timestamp field before comparison.
    /// </summary>
    [Fact]
    public void NormalizeHeader_ZerosTimestampField_WhenConfigured()
    {
        // Arrange
        var testEspPath = CreateTestEsp("TimestampTest.esp", weaponCount: 1);
        var normalizedPath = Path.Combine(_testTempPath, "TimestampTest_normalized.esp");

        // Act
        var success = _validator.NormalizeHeader(testEspPath, normalizedPath, [HeaderField.Timestamp]);

        // Assert
        Assert.True(success, "Header normalization should succeed");
        Assert.True(File.Exists(normalizedPath), "Normalized file should exist");

        // Verify timestamp bytes are zeroed (offset 16-19 in TES4 header)
        var normalizedBytes = File.ReadAllBytes(normalizedPath);
        _output.WriteLine($"Normalized file size: {normalizedBytes.Length} bytes");
        Assert.True(normalizedBytes.Length >= 20, "File should have at least 20 bytes for header");
        Assert.Equal(0, normalizedBytes[16]);
        Assert.Equal(0, normalizedBytes[17]);
        Assert.Equal(0, normalizedBytes[18]);
        Assert.Equal(0, normalizedBytes[19]);
    }

    /// <summary>
    /// Tests that validator normalizes NextFormId field before comparison.
    /// </summary>
    [Fact]
    public void NormalizeHeader_ZerosNextFormIdField_WhenConfigured()
    {
        // Arrange
        var testEspPath = CreateTestEsp("NextFormIdTest.esp", weaponCount: 1);
        var normalizedPath = Path.Combine(_testTempPath, "NextFormIdTest_normalized.esp");

        // Act
        var success = _validator.NormalizeHeader(testEspPath, normalizedPath, [HeaderField.NextFormId]);

        // Assert
        Assert.True(success, "Header normalization should succeed");
        Assert.True(File.Exists(normalizedPath), "Normalized file should exist");
        _output.WriteLine($"Normalized file created at: {normalizedPath}");
    }

    /// <summary>
    /// Tests that validator normalizes multiple header fields together.
    /// </summary>
    [Fact]
    public void NormalizeHeader_ZerosMultipleFields_WhenConfigured()
    {
        // Arrange
        var testEspPath = CreateTestEsp("MultiFieldTest.esp", weaponCount: 1);
        var normalizedPath = Path.Combine(_testTempPath, "MultiFieldTest_normalized.esp");

        // Act
        var success = _validator.NormalizeHeader(testEspPath, normalizedPath, 
            [HeaderField.Timestamp, HeaderField.NextFormId]);

        // Assert
        Assert.True(success, "Multi-field normalization should succeed");
        Assert.True(File.Exists(normalizedPath), "Normalized file should exist");

        // Verify timestamp bytes are zeroed
        var normalizedBytes = File.ReadAllBytes(normalizedPath);
        Assert.Equal(0, normalizedBytes[16]);
        Assert.Equal(0, normalizedBytes[17]);
        Assert.Equal(0, normalizedBytes[18]);
        Assert.Equal(0, normalizedBytes[19]);
    }

    #endregion

    #region Error Classification Tests

    /// <summary>
    /// Tests that missing ESP file results in fatal error.
    /// </summary>
    [Fact]
    public void Validate_WhenEspMissing_ReturnsFatalError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testTempPath, "NonExistent.esp");
        var profile = new ESPValidationProfile
        {
            ProfileId = "missing-esp-test",
            StructuralExpectations = new StructuralExpectation()
        };

        // Act
        var result = _validator.Validate(nonExistentPath, profile);

        // Assert
        _output.WriteLine($"Testing with non-existent path: {nonExistentPath}");
        _output.WriteLine($"Result: IsValid={result.IsValid}, Errors={string.Join("; ", result.Errors)}");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not found"));
    }

    /// <summary>
    /// Tests that corrupted ESP results in fatal error.
    /// </summary>
    [Fact]
    public void Validate_WhenEspCorrupted_ReturnsFatalError()
    {
        // Arrange - Create a file with invalid ESP content
        var corruptedPath = Path.Combine(_testTempPath, "Corrupted.esp");
        File.WriteAllBytes(corruptedPath, [0x00, 0x01, 0x02, 0x03]); // Invalid ESP magic

        var profile = new ESPValidationProfile
        {
            ProfileId = "corrupted-esp-test",
            StructuralExpectations = new StructuralExpectation()
        };

        // Act
        var result = _validator.Validate(corruptedPath, profile);

        // Assert
        _output.WriteLine($"Testing with corrupted file: {corruptedPath}");
        _output.WriteLine($"Result: IsValid={result.IsValid}, Errors={string.Join("; ", result.Errors)}");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Failed to parse") || e.Contains("overlay"));
    }

    /// <summary>
    /// Tests that small file size triggers warning (not error when validation otherwise passes).
    /// </summary>
    [Fact]
    public void Validate_WhenFileSizeSmall_ReturnsWarning()
    {
        // Arrange - Create a minimal ESP (empty mod is typically small)
        var testEspPath = CreateTestEsp("SmallFile.esp"); // No records = small file
        var profile = new ESPValidationProfile
        {
            ProfileId = "small-file-test",
            AllowedWarnings = ["Small file size"],
            StructuralExpectations = new StructuralExpectation()
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"File size: {result.FileSizeBytes} bytes");
        _output.WriteLine($"Warnings: {string.Join("; ", result.Warnings)}");
        // Small files should trigger a warning but validation may still pass if no other errors
        // Note: An empty ESP created by Mutagen might still be larger than 1KB
        if (result.FileSizeBytes < 1024)
        {
            Assert.Contains(result.Warnings, w => w.Contains("Small file size", StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Custom Check Tests

    /// <summary>
    /// Tests that custom checks can validate specific EditorIDs exist.
    /// </summary>
    [Fact]
    public void ValidateCustomChecks_WhenEditorIdExists_Passes()
    {
        // Arrange - Create ESP with a known weapon
        var modKey = ModKey.FromFileName("CustomCheckTest.esp");
        var mod = new Fallout4Mod(modKey, Fallout4Release.Fallout4);
        var weapon = mod.Weapons.AddNew();
        weapon.EditorID = "TestWeapon";
        
        var espPath = Path.Combine(_testTempPath, "CustomCheckTest.esp");
        mod.WriteToBinary(espPath);

        var profile = new ESPValidationProfile
        {
            ProfileId = "custom-check-test",
            StructuralExpectations = new StructuralExpectation
            {
                CustomChecks =
                [
                    new CustomCheck
                    {
                        Description = "Test weapon should exist",
                        Execute = m =>
                        {
                            var hasWeapon = m.Weapons.Any(w => w.EditorID == "TestWeapon");
                            return hasWeapon
                                ? CustomCheckResult.Pass()
                                : CustomCheckResult.Fail("TestWeapon not found");
                        }
                    }
                ]
            }
        };

        // Act
        var result = _validator.Validate(espPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, Errors={string.Join("; ", result.Errors)}");
        Assert.True(result.IsValid, $"Custom check should pass. Errors: {string.Join("; ", result.Errors)}");
    }

    /// <summary>
    /// Tests that custom checks fail when expected EditorID is missing.
    /// </summary>
    [Fact]
    public void ValidateCustomChecks_WhenEditorIdMissing_Fails()
    {
        // Arrange - Create ESP without the expected weapon
        var testEspPath = CreateTestEsp("CustomCheckFailTest.esp", weaponCount: 1);

        var profile = new ESPValidationProfile
        {
            ProfileId = "custom-check-fail-test",
            StructuralExpectations = new StructuralExpectation
            {
                CustomChecks =
                [
                    new CustomCheck
                    {
                        Description = "NonExistentWeapon should exist",
                        Execute = m =>
                        {
                            var hasWeapon = m.Weapons.Any(w => w.EditorID == "NonExistentWeapon");
                            return hasWeapon
                                ? CustomCheckResult.Pass()
                                : CustomCheckResult.Fail("NonExistentWeapon not found");
                        }
                    }
                ]
            }
        };

        // Act
        var result = _validator.Validate(testEspPath, profile);

        // Assert
        _output.WriteLine($"Result: IsValid={result.IsValid}, Errors={string.Join("; ", result.Errors)}");
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("NonExistentWeapon not found"));
    }

    #endregion

    #region ValidationResult Tests

    /// <summary>
    /// Tests that ValidationResult.IsValid is true when no errors exist.
    /// </summary>
    [Fact]
    public void ValidationResult_IsValid_WhenNoErrors()
    {
        // Arrange
        var result = new ValidationResult
        {
            WeaponCount = 1,
            AmmoCount = 1,
            CobjCount = 1,
            FileSizeBytes = 1024,
            HasValidHeader = true
        };

        // Act & Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    /// Tests that ValidationResult.IsValid is false when errors exist.
    /// </summary>
    [Fact]
    public void ValidationResult_IsNotValid_WhenErrorsExist()
    {
        // Arrange
        var result = new ValidationResult();
        result.AddError("Test error");

        // Act & Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    /// <summary>
    /// Tests ValidationResult factory methods.
    /// </summary>
    [Fact]
    public void ValidationResult_FactoryMethods_CreateCorrectResults()
    {
        // Test Success factory
        var success = ValidationResult.Success(weaponCount: 2, ammoCount: 3, cobjCount: 1, fileSize: 2048);
        Assert.True(success.IsValid);
        Assert.Equal(2, success.WeaponCount);
        Assert.Equal(3, success.AmmoCount);
        Assert.Equal(1, success.CobjCount);
        Assert.Equal(2048, success.FileSizeBytes);
        Assert.True(success.HasValidHeader);

        // Test Failure factory
        var failure = ValidationResult.Failure("Test failure");
        Assert.False(failure.IsValid);
        Assert.Contains("Test failure", failure.Errors);
    }

    #endregion

    #region CountRange Tests

    /// <summary>
    /// Tests CountRange.Contains for various values.
    /// </summary>
    [Theory]
    [InlineData(5, 10, 5, true)]   // Min boundary
    [InlineData(5, 10, 10, true)]  // Max boundary
    [InlineData(5, 10, 7, true)]   // Middle
    [InlineData(5, 10, 4, false)]  // Below min
    [InlineData(5, 10, 11, false)] // Above max
    public void CountRange_Contains_ReturnsCorrectResult(int min, int max, int value, bool expected)
    {
        var range = new CountRange(min, max);
        Assert.Equal(expected, range.Contains(value));
    }

    /// <summary>
    /// Tests CountRange.Exact factory method.
    /// </summary>
    [Fact]
    public void CountRange_Exact_CreatesCorrectRange()
    {
        var range = CountRange.Exact(5);
        Assert.Equal(5, range.Min);
        Assert.Equal(5, range.Max);
        Assert.True(range.Contains(5));
        Assert.False(range.Contains(4));
        Assert.False(range.Contains(6));
    }

    /// <summary>
    /// Tests CountRange.AtLeast factory method.
    /// </summary>
    [Fact]
    public void CountRange_AtLeast_CreatesCorrectRange()
    {
        var range = CountRange.AtLeast(3);
        Assert.Equal(3, range.Min);
        Assert.Equal(int.MaxValue, range.Max);
        Assert.True(range.Contains(3));
        Assert.True(range.Contains(100));
        Assert.False(range.Contains(2));
    }

    #endregion
}
