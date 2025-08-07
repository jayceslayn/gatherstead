namespace Gatherstead.Db.Encryption;

public interface IEncryptionKeyProvider
{
    byte[] GetCurrentKey();
}
