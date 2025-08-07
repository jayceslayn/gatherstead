using System;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Gatherstead.Db.Encryption;

public class EncryptedStringConverter : ValueConverter<string, byte[]>
{
    public EncryptedStringConverter() : base(
        v => EncryptionHelper.Encrypt(v ?? string.Empty),
        v => EncryptionHelper.Decrypt(v))
    { }
}

public class EncryptedDateOnlyConverter : ValueConverter<DateOnly?, byte[]>
{
    public EncryptedDateOnlyConverter() : base(
        v => v.HasValue ? EncryptionHelper.Encrypt(v.Value.ToString("O")) : Array.Empty<byte>(),
        v => v.Length == 0 ? null : DateOnly.Parse(EncryptionHelper.Decrypt(v)))
    { }
}
