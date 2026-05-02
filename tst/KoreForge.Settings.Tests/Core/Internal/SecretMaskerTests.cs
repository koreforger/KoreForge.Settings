using FluentAssertions;
using KoreForge.Settings.Core.Internal;

namespace KoreForge.Settings.Tests.Core.Internal;

public class SecretMaskerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("abc", "***")]
    [InlineData("abcd", "****")]
    [InlineData("abcdef", "ab**ef")]
    [InlineData("abcdefgh", "ab****gh")]
    public void Mask_ReturnsExpectedStrings(string? input, string expected)
    {
        SecretMasker.Mask(input).Should().Be(expected);
    }
}
