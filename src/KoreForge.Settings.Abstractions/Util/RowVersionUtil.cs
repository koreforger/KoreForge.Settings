namespace KoreForge.Settings.Util;

public static class RowVersionUtil
{
    public static string ToHex(byte[] rowVersion) => Convert.ToHexString(rowVersion);
    public static byte[] FromHex(string hex) => Convert.FromHexString(hex);
}
