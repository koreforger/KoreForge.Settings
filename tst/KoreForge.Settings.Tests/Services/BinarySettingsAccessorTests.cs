using FluentAssertions;
using KoreForge.Settings.Core.Services;

namespace KoreForge.Settings.Tests.Services;

public class BinarySettingsAccessorTests
{
    [Fact]
    public void Given_BinarySnapshot_When_TryGet_Then_Succeeds()
    {
        var accessor = new BinarySettingsAccessor();
        accessor.SetSnapshot(System.Collections.Immutable.ImmutableDictionary<string, byte[]>.Empty.Add("K", new byte[] { 1, 2, 3 }));
        accessor.TryGet("K", out var mem).Should().BeTrue();
        mem.Length.Should().Be(3);
    }

    [Fact]
    public void Given_Base64Request_When_GetAsBase64Url_Then_Formats()
    {
        var accessor = new BinarySettingsAccessor();
        accessor.SetSnapshot(System.Collections.Immutable.ImmutableDictionary<string, byte[]>.Empty.Add("K", new byte[] { 255 }));
        // 0xFF -> /w== base64 -> trim == => /w -> url safe => _w
        accessor.GetAsBase64Url("K").Should().Be("_w");
    }
}
