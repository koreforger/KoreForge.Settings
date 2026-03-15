namespace KF.Settings.Models;

public enum BinaryEncoding
{
    Base64Url = 0,
    UuEncode = 1
}

public enum SettingOperation
{
    Insert,
    Update,
    Delete,
    Rollback
}
