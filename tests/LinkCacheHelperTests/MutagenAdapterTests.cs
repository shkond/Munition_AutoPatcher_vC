
using System;
using Moq;
using MunitionAutoPatcher.Services.Implementations;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Xunit;

namespace LinkCacheHelperTests
{
    public class MutagenAdapterTests
    {
        // Mock for the underlying IGameEnvironment that also implements IDisposable
        public class DisposableGameEnvironment : IGameEnvironment<IFallout4Mod, IFallout4ModGetter>, IDisposable
        {
            public bool IsDisposed { get; private set; } = false;
            public void Dispose() => IsDisposed = true;

            // Implement the rest of the interface with default/mocked behavior
            public IGameEnvironment<IFallout4Mod, IFallout4ModGetter> Duplicate() => throw new NotImplementedException();
            public ILinkCache<IFallout4Mod, IFallout4ModGetter> LinkCache => throw new NotImplementedException();
            public ILoadOrder<IModListing<IFallout4ModGetter>> LoadOrder => throw new NotImplementedException();
            public string GameRelease => throw new NotImplementedException();
        }

        [Fact]
        public void Adapter_Calls_Dispose_On_Disposable_Environment()
        {
            // Arrange
            var disposableEnv = new DisposableGameEnvironment();
            var adapter = new MutagenV51EnvironmentAdapter(disposableEnv);

            // Act
            adapter.Dispose();

            // Assert
            Assert.True(disposableEnv.IsDisposed);
        }

        [Fact]
        public void Adapter_DoesNotThrow_On_NonDisposable_Environment()
        {
            // Arrange
            var nonDisposableEnv = new Mock<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>>().Object;
            var adapter = new MutagenV51EnvironmentAdapter(nonDisposableEnv);

            // Act
            var ex = Record.Exception(() => adapter.Dispose());

            // Assert
            Assert.Null(ex);
        }
    }
}
