using KF.Settings.Interfaces;

namespace KF.Settings.Encryption;

public sealed class NoOpEncryptionProvider : IEncryptionProvider
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
}
