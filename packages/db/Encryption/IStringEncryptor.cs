using System;

namespace Gatherstead.Db.Encryption;

public interface IStringEncryptor
{
    byte[] Encrypt(string plaintext);
    string Decrypt(byte[] cipherText);
}
