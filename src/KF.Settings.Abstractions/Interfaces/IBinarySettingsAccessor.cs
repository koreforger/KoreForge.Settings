namespace KF.Settings.Interfaces;

public interface IBinarySettingsAccessor
{
    bool TryGet(string key, out ReadOnlyMemory<byte> bytes);
    string GetAsBase64Url(string key);
    string GetAsUuencode(string key);
}
