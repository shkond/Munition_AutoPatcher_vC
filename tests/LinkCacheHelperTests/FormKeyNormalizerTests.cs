using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Plugins;
using Xunit;

namespace LinkCacheHelperTests;

/// <summary>
/// Tests for FormKeyNormalizer to verify FormKey conversion and normalization.
/// Note: Mutagen's ModKey constructor expects fileName WITHOUT extension, and adds extension based on ModType.
/// Also, FormKey.ID is a 24-bit value (masked from the input FormId).
/// </summary>
public class FormKeyNormalizerTests
{
    #region ToMutagenFormKey Tests

    [Fact]
    public void ToMutagenFormKey_WithValidEsmPlugin_ReturnsMasterModType()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "Fallout4.esm", 
            FormId = 0x0001F278 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        // ModKey.FileName includes the extension added by ModKey constructor
        Assert.Contains("Fallout4", result.Value.ModKey.FileName.String);
        Assert.Equal(ModType.Master, result.Value.ModKey.Type);
        // FormKey.ID is 24-bit masked
        Assert.Equal(0x0001F278u & 0x00FFFFFFu, result.Value.ID);
    }

    [Fact]
    public void ToMutagenFormKey_WithValidEspPlugin_ReturnsPluginModType()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "MyMod.esp", 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        Assert.Contains("MyMod", result.Value.ModKey.FileName.String);
        Assert.Equal(ModType.Plugin, result.Value.ModKey.Type);
        Assert.Equal(0x00001234u, result.Value.ID);
    }

    [Fact]
    public void ToMutagenFormKey_WithValidEslPlugin_ReturnsLightModType()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "ccBGSFO4001-PipBoy(Black).esl", 
            FormId = 0x00000800 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        Assert.Contains("ccBGSFO4001-PipBoy(Black)", result.Value.ModKey.FileName.String);
        Assert.Equal(ModType.Light, result.Value.ModKey.Type);
        Assert.Equal(0x00000800u, result.Value.ID);
    }

    [Fact]
    public void ToMutagenFormKey_WithNullFormKey_ReturnsNull()
    {
        var result = FormKeyNormalizer.ToMutagenFormKey(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ToMutagenFormKey_WithNullPluginName_ReturnsNull()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = null!, 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);
        Assert.Null(result);
    }

    [Fact]
    public void ToMutagenFormKey_WithEmptyPluginName_ReturnsNull()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "", 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);
        Assert.Null(result);
    }

    [Fact]
    public void ToMutagenFormKey_WithWhitespacePluginName_ReturnsNull()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "   ", 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);
        Assert.Null(result);
    }

    [Fact]
    public void ToMutagenFormKey_WithZeroFormId_ReturnsNull()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "Valid.esp", 
            FormId = 0 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("FALLOUT4.ESM")]
    [InlineData("fallout4.esm")]
    [InlineData("Fallout4.ESM")]
    public void ToMutagenFormKey_WithDifferentCasing_DetectsModTypeCorrectly(string pluginName)
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = pluginName, 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        Assert.Equal(ModType.Master, result.Value.ModKey.Type);
    }

    [Fact]
    public void ToMutagenFormKey_WithPathPrefix_ExtractsFileName()
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = @"C:\Games\Fallout4\Data\MyMod.esp", 
            FormId = 0x00001234 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        Assert.Contains("MyMod", result.Value.ModKey.FileName.String);
        Assert.Equal(ModType.Plugin, result.Value.ModKey.Type);
    }

    [Theory]
    [InlineData(0x00000001u, 0x00000001u)]  // Normal small FormID
    [InlineData(0x00FFFFFFu, 0x00FFFFFFu)]  // Max 24-bit value
    [InlineData(0x01000000u, 0x00000000u)]  // Over 24-bit, masked to 0
    [InlineData(0xFFFFFFFFu, 0x00FFFFFFu)]  // Max uint32, masked to 24-bit max
    public void ToMutagenFormKey_WithVariousFormIds_MasksTo24Bits(uint inputFormId, uint expectedId)
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "Test.esp", 
            FormId = inputFormId 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        // FormId 0 after masking returns null
        if (expectedId == 0)
        {
            // The normalizer checks for fk.FormId == 0 before masking occurs in FormKey constructor
            // So this should still return a result since input is non-zero
            Assert.NotNull(result);
            Assert.Equal(expectedId, result.Value.ID);
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(expectedId, result.Value.ID);
        }
    }

    #endregion

    #region NormalizePluginName Tests

    [Fact]
    public void NormalizePluginName_WithValidFileName_ReturnsUnchanged()
    {
        var result = FormKeyNormalizer.NormalizePluginName("MyMod.esp");
        Assert.Equal("MyMod.esp", result);
    }

    [Fact]
    public void NormalizePluginName_WithPathPrefix_ExtractsFileName()
    {
        var result = FormKeyNormalizer.NormalizePluginName(@"C:\Games\Fallout4\Data\MyMod.esp");
        Assert.Equal("MyMod.esp", result);
    }

    [Fact]
    public void NormalizePluginName_WithNoExtension_AddsEsp()
    {
        var result = FormKeyNormalizer.NormalizePluginName("MyMod");
        Assert.Equal("MyMod.esp", result);
    }

    [Fact]
    public void NormalizePluginName_WithNull_ReturnsEmpty()
    {
        var result = FormKeyNormalizer.NormalizePluginName(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizePluginName_WithEmpty_ReturnsEmpty()
    {
        var result = FormKeyNormalizer.NormalizePluginName("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void NormalizePluginName_WithWhitespace_ReturnsEmpty()
    {
        var result = FormKeyNormalizer.NormalizePluginName("   ");
        Assert.Equal(string.Empty, result);
    }

    [Theory]
    [InlineData("Fallout4.esm", "Fallout4.esm")]
    [InlineData("Test.esl", "Test.esl")]
    [InlineData("Mod.esp", "Mod.esp")]
    public void NormalizePluginName_WithValidExtension_PreservesExtension(string input, string expected)
    {
        var result = FormKeyNormalizer.NormalizePluginName(input);
        Assert.Equal(expected, result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ToMutagenFormKey_ProducesValidFormKey()
    {
        var original = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = "TestMod.esp", 
            FormId = 0x00ABCDEF 
        };

        var mutagenFk = FormKeyNormalizer.ToMutagenFormKey(original);
        Assert.NotNull(mutagenFk);

        // Verify the FormKey is valid and can be used
        Assert.False(mutagenFk.Value.IsNull);
        Assert.Equal(0x00ABCDEFu, mutagenFk.Value.ID);
    }

    [Theory]
    [InlineData("DLCRobot.esm", 0x00001000u)]
    [InlineData("DLCCoast.esm", 0x00002000u)]
    [InlineData("DLCNukaWorld.esm", 0x00003000u)]
    [InlineData("ccBGSFO4003-PipBoy(Camo01).esl", 0x00000801u)]
    public void ToMutagenFormKey_DlcPlugins_ParseCorrectly(string pluginName, uint formId)
    {
        var customFk = new MunitionAutoPatcher.Models.FormKey 
        { 
            PluginName = pluginName, 
            FormId = formId 
        };

        var result = FormKeyNormalizer.ToMutagenFormKey(customFk);

        Assert.NotNull(result);
        Assert.False(result.Value.IsNull);
        Assert.Equal(formId, result.Value.ID);
        
        // Check ModType based on extension
        if (pluginName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(ModType.Master, result.Value.ModKey.Type);
        else if (pluginName.EndsWith(".esl", StringComparison.OrdinalIgnoreCase))
            Assert.Equal(ModType.Light, result.Value.ModKey.Type);
        else
            Assert.Equal(ModType.Plugin, result.Value.ModKey.Type);
    }

    #endregion
}
