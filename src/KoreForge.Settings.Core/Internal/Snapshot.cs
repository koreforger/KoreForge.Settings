using System.Collections.Immutable;

namespace KoreForge.Settings.Core.Internal;

internal sealed class Snapshot
{
    public ImmutableDictionary<string, string?> Values { get; }
    public ImmutableDictionary<string, byte[]> Binary { get; }
    public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    public long RowCount { get; }
    public string HashHex { get; }

    public Snapshot(IDictionary<string, string?> values, IDictionary<string, byte[]> binary, long rowCount, string hashHex)
    {
        Values = values.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        Binary = binary.ToImmutableDictionary(StringComparer.OrdinalIgnoreCase);
        RowCount = rowCount;
        HashHex = hashHex;
    }
}
