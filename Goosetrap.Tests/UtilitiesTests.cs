using Goosetrap.Enums;

namespace Goosetrap.Tests
{
    public class UtilitiesTests
    {
        [Theory]
        [InlineData("0.739.0.7390687", "0.739.0.7390687")]
        [InlineData("v1.2.0", "1.2.0")]
        [InlineData("1.1.0", "1.1.0")]
        [InlineData("v2.9.0.0-something", "2.9.0.0")]
        public void GetVersionFromString_parses_roblox_and_app_versions(string input, string expected)
        {
            Assert.Equal(new Version(expected), Utilities.GetVersionFromString(input));
        }

        [Theory]
        [InlineData("")]
        [InlineData("version-4310300497aa4917")]
        [InlineData("not a version")]
        public void GetVersionFromString_falls_back_to_zero(string input)
        {
            Assert.Equal(new Version("0.0.0.0"), Utilities.GetVersionFromString(input));
        }

        [Theory]
        [InlineData("1.2.0", "1.3.0", -1)]
        [InlineData("1.3.0", "1.2.0", 1)]
        [InlineData("1.3.0", "1.3.0", 0)]
        public void CompareVersions_compares_versions(string first, string second, int expected)
        {
            Assert.Equal(expected, (int)Utilities.CompareVersions(first, second));
        }

        [Fact]
        public void CompareVersions_handles_real_roblox_versions()
        {
            Assert.Equal(1, (int)Utilities.CompareVersions("0.739.0.7390687", "0.738.0.7380000"));
            Assert.Equal(-1, (int)Utilities.CompareVersions("0.738.0.7380000", "0.739.0.7390687"));
        }
    }
}