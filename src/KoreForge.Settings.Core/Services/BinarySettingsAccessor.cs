using KoreForge.Settings.Interfaces;
using System.Collections.Immutable;

namespace KoreForge.Settings.Core.Services;

public sealed class BinarySettingsAccessor : IBinarySettingsAccessor
{
    private ImmutableDictionary<string, byte[]> _current = ImmutableDictionary<string, byte[]>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);

    public void SetSnapshot(ImmutableDictionary<string, byte[]> binary)
    {
        _current = binary;
    }

    public bool TryGet(string key, out ReadOnlyMemory<byte> bytes)
    {
        if (_current.TryGetValue(key, out var arr))
        {
            bytes = arr;
            return true;
        }
        bytes = default;
        return false;
    }

    public string GetAsBase64Url(string key)
    {
        if (!TryGet(key, out var mem)) throw new KeyNotFoundException(key);
        return Convert.ToBase64String(mem.Span).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public string GetAsUuencode(string key)
    {
        if (!TryGet(key, out var mem)) throw new KeyNotFoundException(key);
        return UuEncode(mem.Span);
    }

    private static string UuEncode(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return string.Empty;
        var sb = new System.Text.StringBuilder();
        int idx = 0;
        while (idx < data.Length)
        {
            int chunk = Math.Min(45, data.Length - idx);
            sb.Append((char)(' ' + (chunk & 0x3F)));
            for (int i = 0; i < chunk; i += 3)
            {
                int b1 = data[idx + i];
                int b2 = i + 1 < chunk ? data[idx + i + 1] : 0;
                int b3 = i + 2 < chunk ? data[idx + i + 2] : 0;
                uint tuple = (uint)((b1 << 16) | (b2 << 8) | b3);
                sb.Append((char)(' ' + ((tuple >> 18) & 0x3F)));
                sb.Append((char)(' ' + ((tuple >> 12) & 0x3F)));
                sb.Append((char)(' ' + ((tuple >> 6) & 0x3F)));
                sb.Append((char)(' ' + (tuple & 0x3F)));
            }
            sb.Append('\n');
            idx += chunk;
        }
        sb.Append('`').Append('\n');
        return sb.ToString();
    }
}
