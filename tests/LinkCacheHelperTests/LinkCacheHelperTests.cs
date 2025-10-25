using Xunit;

namespace LinkCacheHelperTests;

public class TryResolveTests
{
    [Fact]
    public void ReturnsNull_When_LinkLikeIsNull()
    {
        var res = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(null, new object());
        Assert.Null(res);
    }

    [Fact]
    public void ReturnsNull_When_LinkCacheDoesNotHaveTryResolve()
    {
        // Create a dummy object that does not implement TryResolve
        var dummyLink = new { Something = 1 };
        var dummyCache = new { NoResolve = true };
        var res = MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(dummyLink, dummyCache);
        Assert.Null(res);
    }

    [Fact]
    public void DoesNotThrow_For_InvalidInputs()
    {
        var dummyLink = new object();
        var dummyCache = new object();
        var ex = Record.Exception(() => MunitionAutoPatcher.Services.Implementations.LinkCacheHelper.TryResolveViaLinkCache(dummyLink, dummyCache));
        Assert.Null(ex);
    }
}
