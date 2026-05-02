using KoreForge.Settings.Interfaces;

namespace KoreForge.Settings.Encryption;

public sealed class NoOpEncryptionProvider : IEncryptionProvider
{
    public string Encrypt(string plaintext) => plaintext;
    public string Decrypt(string ciphertext) => ciphertext;
}
