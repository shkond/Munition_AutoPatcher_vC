
using System;
using Moq;
using MunitionAutoPatcher.Services.Implementations;
using Microsoft.Extensions.Logging.Abstractions;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Fallout4;
using Xunit;

namespace LinkCacheHelperTests
{
    public class MutagenAdapterTests
    {
        [Fact]
        public void Adapter_Calls_Dispose_On_Disposable_Environment()
        {
            // Arrange
            var disposed = false;
            var mockEnv = new Mock<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>>();
            mockEnv.As<IDisposable>().Setup(d => d.Dispose()).Callback(() => disposed = true);
            var adapter = new MutagenV51EnvironmentAdapter(mockEnv.Object, NullLogger<MutagenV51EnvironmentAdapter>.Instance);

            // Act
            adapter.Dispose();

            // Assert
            Assert.True(disposed);
        }

        [Fact]
        public void Adapter_DoesNotThrow_On_NonDisposable_Environment()
        {
            // Arrange
            var nonDisposableEnv = new Mock<IGameEnvironment<IFallout4Mod, IFallout4ModGetter>>().Object;
            var adapter = new MutagenV51EnvironmentAdapter(nonDisposableEnv, NullLogger<MutagenV51EnvironmentAdapter>.Instance);

            // Act
            var ex = Record.Exception(() => adapter.Dispose());

            // Assert
            Assert.Null(ex);
        }
    }
}
