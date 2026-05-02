namespace KoreForge.Settings.Interfaces;

public interface IEncryptionProvider
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
