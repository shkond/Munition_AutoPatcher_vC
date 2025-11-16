using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkCacheHelperTests;

public class TryResolveTests
{
    [Theory]
    [InlineData(null, "validCache", "Null link parameter")]
    [InlineData("validLink", null, "Null cache parameter")]
    [InlineData(null, null, "Both parameters null")]
    public void TryResolveViaLinkCache_WhenParametersAreNull_ReturnsNull(object? linkLike, object? cache, string scenario)
    {
        // Arrange
        // Parameters are provided by the theory data

        // Act
        var result = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(linkLike, cache);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void TryResolveViaLinkCache_WhenLinkCacheDoesNotHaveTryResolve_ReturnsNull()
    {
        // Arrange
        var dummyLink = new { Something = 1 };
        var dummyCache = new { NoResolve = true };

        // Act
        var result = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(dummyLink, dummyCache);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("invalidLink", "invalidCache")]
    [InlineData(123, 456)]
    [InlineData(true, false)]
    public void TryResolveViaLinkCache_WhenInputsAreInvalid_DoesNotThrow(object invalidLink, object invalidCache)
    {
        // Arrange
        // Parameters are provided by the theory data

        // Act & Assert
        var exception = Record.Exception(() => MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(invalidLink, invalidCache));
        Assert.Null(exception);
    }
}
