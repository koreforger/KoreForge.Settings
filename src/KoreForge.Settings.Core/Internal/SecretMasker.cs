namespace KoreForge.Settings.Core.Internal;

internal static class SecretMasker
{
    public static string Mask(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var len = value.Length;
        return len switch
        {
            <= 4 => new string('*', len),
            _ => value[..2] + new string('*', len - 4) + value[^2..]
        };
    }
}
