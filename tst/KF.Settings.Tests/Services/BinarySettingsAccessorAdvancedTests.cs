using FluentAssertions;
using KF.Settings.Core.Services;

namespace KF.Settings.Tests.Services;

public class BinarySettingsAccessorAdvancedTests
{
    [Fact]
    public void Given_MissingKey_When_TryGet_Then_False()
    {
        var acc = new BinarySettingsAccessor();
        acc.TryGet("Nope", out var _).Should().BeFalse();
    }

    [Fact]
    public void Given_MissingKey_When_GetAsBase64Url_Then_KeyNotFound()
    {
        var acc = new BinarySettingsAccessor();
        Action act = () => acc.GetAsBase64Url("X");
        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Given_Binary_When_GetAsUuencode_Then_EndsWithBacktick()
    {
        var acc = new BinarySettingsAccessor();
        acc.SetSnapshot(System.Collections.Immutable.ImmutableDictionary<string, byte[]>.Empty.Add("K", new byte[] { 1, 2, 3, 4, 5 }));
        var text = acc.GetAsUuencode("K");
        text.Trim().EndsWith("`").Should().BeTrue();
    }
}
