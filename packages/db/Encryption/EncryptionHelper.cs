namespace Gatherstead.Db.Encryption;

public static class EncryptionHelper
{
    public static IStringEncryptor Encryptor { get; set; } = new NoOpEncryptor();

    public static byte[] Encrypt(string plaintext) => Encryptor.Encrypt(plaintext);
    public static string Decrypt(byte[] cipherText) => Encryptor.Decrypt(cipherText);

    private class NoOpEncryptor : IStringEncryptor
    {
        public byte[] Encrypt(string plaintext) => System.Text.Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
        public string Decrypt(byte[] cipherText) => System.Text.Encoding.UTF8.GetString(cipherText);
    }
}
