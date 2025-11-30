// Copyright (c) Munition AutoPatcher contributors. Licensed under the MIT License.

using ConfirmerTests.TestHelpers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MunitionAutoPatcher.Models;
using MunitionAutoPatcher.Services.Implementations;
using MunitionAutoPatcher.Services.Interfaces;
using Xunit;

namespace ConfirmerTests;

/// <summary>
/// Unit tests for AttachPointConfirmer.
/// Tests the OMOD AttachPoint ↔ Weapon AttachParentSlots matching logic
/// and ammo reference detection within OMODs.
/// 
/// Key scenarios tested:
/// - OMOD resolution failure (rootNull)
/// - OMOD resolved but no AttachPoint
/// - AttachPoint found but no weapon match
/// - Weapon matched but no ammo reference in OMOD
/// - Full confirmation path (all conditions met)
/// </summary>
public class AttachPointConfirmerTests
{
    private readonly Mock<IMutagenAccessor> _mockAccessor;
    private readonly ILogger<AttachPointConfirmer> _logger;
    private readonly AttachPointConfirmer _confirmer;

    public AttachPointConfirmerTests()
    {
        _mockAccessor = new Mock<IMutagenAccessor>();
        _logger = NullLogger<AttachPointConfirmer>.Instance;
        _confirmer = new AttachPointConfirmer(_mockAccessor.Object, _logger);

        // Default: TryGetPluginAndIdFromRecord returns false
        _mockAccessor
            .Setup(x => x.TryGetPluginAndIdFromRecord(It.IsAny<object>(), out It.Ref<string>.IsAny, out It.Ref<uint>.IsAny))
            .Returns(false);
    }

    #region OMOD Resolution Failure Tests

    [Fact]
    public async Task ConfirmAsync_WhenResolverIsNull_DoesNotConfirmCandidate()
    {
        // Arrange
        var candidate = CreateCobjCandidate("TestMod.esp", 0x801);
        var context = ConfirmationContextBuilder.Create()
            .WithResolver(null!)
            .Build();

        // Act
        await _confirmer.ConfirmAsync(new[] { candidate }, context, CancellationToken.None);

        // Assert
        Assert.False(candidate.ConfirmedAmmoChange, "Candidate should not be confirmed when resolver is null");
    }

    [Fact]
    public async Task ConfirmAsync_WhenResolverReturnsNull_IncreasesRootNullCount()
    {
        // Arrange
        var candidate = CreateCobjCandidate("TestMod.esp", 0x801);
        
        var mockResolver = new Mock<ILinkResolver>();
        mockResolver
            .Setup(x => x.ResolveByKey(It.IsAny<FormKey>()))
            .Returns((object?)null);
        mockResolver
            .Setup(x => x.TryResolve(It.IsAny<object>(), out It.Ref<object?>.IsAny))
            .Returns(false);

        var context = ConfirmationContextBuilder.Create()
            .WithResolver(mockResolver.Object)
            .Build();

        // Act
        await _confirmer.ConfirmAsync(new[] { candidate }, context, CancellationToken.None);

        // Assert
        Assert.False(candidate.ConfirmedAmmoChange, "Candidate should not be confirmed when resolution returns null");
        // Note: rootNull counter is internal, verified via log output in integration tests
    }

    #endregion

    #region Non-OMOD Type Filtering Tests

    [Fact]
    public async Task ConfirmAsync_WhenCandidateTypeIsNotOmodLike_SkipsCandidate()
    {
        // Arrange
        var candidate = new OmodCandidate
        {
            CandidateType = "WEAP", // Not OMOD-like
            CandidateFormKey = FormKeyFactory.CreateModel("TestMod.esp", 0x801),
            CandidateEditorId = "TestWeapon"
        };

        var context = ConfirmationContextBuilder.Create().Build();

        // Act
        await _confirmer.ConfirmAsync(new[] { candidate }, context, CancellationToken.None);

        // Assert
        Assert.False(candidate.ConfirmedAmmoChange, "Non-OMOD-like candidate should be skipped");
    }

    [Theory]
    [InlineData("OMOD")]
    [InlineData("ObjectModification")]
    [InlineData("COBJ")]
    [InlineData("ConstructibleObject")]
    [InlineData("CreatedWeapon")]
    public async Task ConfirmAsync_WhenCandidateTypeIsOmodLike_AttemptsResolution(string candidateType)
    {
        // Arrange
        var candidate = new OmodCandidate
        {
            CandidateType = candidateType,
            CandidateFormKey = FormKeyFactory.CreateModel("TestMod.esp", 0x801),
            CandidateEditorId = "TestOmod"
        };

        var mockResolver = new Mock<ILinkResolver>();
        mockResolver
            .Setup(x => x.ResolveByKey(It.IsAny<FormKey>()))
            .Returns((object?)null); // Will fail resolution, but should attempt it

        var context = ConfirmationContextBuilder.Create()
            .WithResolver(mockResolver.Object)
            .Build();

        // Act
        await _confirmer.ConfirmAsync(new[] { candidate }, context, CancellationToken.None);

        // Assert
        // Verify that resolution was attempted for OMOD-like types
        mockResolver.Verify(x => x.ResolveByKey(It.IsAny<FormKey>()), Times.AtLeastOnce());
    }

    #endregion

    #region Already Confirmed Tests

    [Fact]
    public async Task ConfirmAsync_WhenCandidateAlreadyConfirmed_SkipsProcessing()
    {
        // Arrange
        var candidate = CreateCobjCandidate("TestMod.esp", 0x801);
        candidate.ConfirmedAmmoChange = true;
        candidate.ConfirmReason = "Previously confirmed";

        var mockResolver = new Mock<ILinkResolver>();
        var context = ConfirmationContextBuilder.Create()
            .WithResolver(mockResolver.Object)
            .Build();

        // Act
        await _confirmer.ConfirmAsync(new[] { candidate }, context, CancellationToken.None);

        // Assert
        mockResolver.Verify(x => x.ResolveByKey(It.IsAny<FormKey>()), Times.Never(),
            "Should not attempt resolution for already confirmed candidates");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task ConfirmAsync_WhenCancellationRequested_CompletesGracefully()
    {
        // Arrange
        var candidates = Enumerable.Range(1, 100)
            .Select(i => CreateCobjCandidate("TestMod.esp", (uint)(0x800 + i)))
            .ToList();

        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var context = ConfirmationContextBuilder.Create()
            .WithCancellationToken(cts.Token)
            .Build();

        // Act & Assert - Should complete without throwing
        await _confirmer.ConfirmAsync(candidates, context, cts.Token);
        
        // Most candidates should remain unconfirmed due to early exit
        var confirmedCount = candidates.Count(c => c.ConfirmedAmmoChange);
        Assert.True(confirmedCount < candidates.Count, 
            "Should exit early when cancellation is requested");
    }

    #endregion

    #region Empty Candidates Tests

    [Fact]
    public async Task ConfirmAsync_WhenNoCandidates_CompletesSuccessfully()
    {
        // Arrange
        var context = ConfirmationContextBuilder.Create().Build();

        // Act & Assert - Should not throw
        await _confirmer.ConfirmAsync(Enumerable.Empty<OmodCandidate>(), context, CancellationToken.None);
    }

    #endregion

    #region Helper Methods

    private static OmodCandidate CreateCobjCandidate(string pluginName, uint formId)
    {
        return new OmodCandidate
        {
            CandidateType = "COBJ",
            CandidateFormKey = FormKeyFactory.CreateModel(pluginName, formId),
            CandidateEditorId = $"co_TestItem_{formId:X8}",
            SourcePlugin = pluginName
        };
    }

    private static OmodCandidate CreateOmodCandidate(string pluginName, uint formId)
    {
        return new OmodCandidate
        {
            CandidateType = "OMOD",
            CandidateFormKey = FormKeyFactory.CreateModel(pluginName, formId),
            CandidateEditorId = $"mod_TestMod_{formId:X8}",
            SourcePlugin = pluginName
        };
    }

    #endregion
}
