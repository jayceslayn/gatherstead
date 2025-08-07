using System;
using System.Security.Cryptography;
using System.Text;

namespace Gatherstead.Db.Encryption;

public class AesGcmStringEncryptor : IStringEncryptor
{
    private readonly IEncryptionKeyProvider _keyProvider;

    public AesGcmStringEncryptor(IEncryptionKeyProvider keyProvider)
    {
        _keyProvider = keyProvider;
    }

    public byte[] Encrypt(string plaintext)
    {
        var key = _keyProvider.GetCurrentKey();
        using var aes = new AesGcm(key);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plainBytes = Encoding.UTF8.GetBytes(plaintext ?? string.Empty);
        var cipherBytes = new byte[plainBytes.Length];
        var tag = new byte[16];
        aes.Encrypt(nonce, plainBytes, cipherBytes, tag);
        var result = new byte[nonce.Length + tag.Length + cipherBytes.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
        Buffer.BlockCopy(cipherBytes, 0, result, nonce.Length + tag.Length, cipherBytes.Length);
        return result;
    }

    public string Decrypt(byte[] cipherText)
    {
        var key = _keyProvider.GetCurrentKey();
        using var aes = new AesGcm(key);
        var nonce = new byte[12];
        var tag = new byte[16];
        var cipherBytes = new byte[cipherText.Length - nonce.Length - tag.Length];
        Buffer.BlockCopy(cipherText, 0, nonce, 0, nonce.Length);
        Buffer.BlockCopy(cipherText, nonce.Length, tag, 0, tag.Length);
        Buffer.BlockCopy(cipherText, nonce.Length + tag.Length, cipherBytes, 0, cipherBytes.Length);
        var plainBytes = new byte[cipherBytes.Length];
        aes.Decrypt(nonce, cipherBytes, tag, plainBytes);
        return Encoding.UTF8.GetString(plainBytes);
    }
}
