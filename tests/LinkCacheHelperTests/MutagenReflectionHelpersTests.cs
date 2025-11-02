using System;
using Xunit;

namespace LinkCacheHelperTests
{
    public class MutagenReflectionHelpersTests
    {
        private class Dummy
        {
            public int Value { get; set; } = 5;
            public string Name { get; set; } = "dummy";
            public int GetConstant() => 42;
            public string Echo(string s) => s;
        }

        [Fact]
        public void TryGetPropertyValue_Returns_Value_When_Present()
        {
            var d = new Dummy { Value = 123, Name = "abc" };
            Assert.True(MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<int>(d, "Value", out var v));
            Assert.Equal(123, v);

            Assert.True(MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryGetPropertyValue<string>(d, "Name", out var n));
            Assert.Equal("abc", n);
        }

        [Fact]
        public void TryInvokeMethod_Returns_Result_When_Present()
        {
            var d = new Dummy();
            Assert.True(MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryInvokeMethod(d, "GetConstant", null, out var r1));
            Assert.Equal(42, (int)r1!);

            Assert.True(MunitionAutoPatcher.Utilities.MutagenReflectionHelpers.TryInvokeMethod(d, "Echo", new object?[] { "hello" }, out var r2));
            Assert.Equal("hello", (string)r2!);
        }
    }
}
