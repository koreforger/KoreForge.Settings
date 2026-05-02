using FluentAssertions;
using KoreForge.Settings.Core.Internal;

namespace KoreForge.Settings.Tests.Core.Internal;

public class SnapshotTests
{
    [Fact]
    public void Snapshot_NormalizesKeysAndExposesMetadata()
    {
        var values = new Dictionary<string, string?> { ["Foo"] = "Bar" };
        var binary = new Dictionary<string, byte[]> { ["Blob"] = new byte[] { 1, 2, 3 } };
        var snapshot = new Snapshot(values, binary, rowCount: 2, hashHex: "AA");

        snapshot.Values["foo"].Should().Be("Bar");
        snapshot.Binary["blob"].Should().BeEquivalentTo(new byte[] { 1, 2, 3 });
        snapshot.RowCount.Should().Be(2);
        snapshot.HashHex.Should().Be("AA");
        snapshot.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}
