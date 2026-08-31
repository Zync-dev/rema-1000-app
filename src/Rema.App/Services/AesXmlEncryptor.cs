using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.XmlEncryption;

namespace Rema.App.Services;

/// <summary>
/// Holder AES-nøglen (32 bytes) der krypterer Data Protection-nøgleringen i databasen.
/// Sættes fra miljøvariablen DATAPROTECTION_KEY (base64). Uden den gemmes
/// nøgleringen i klartekst – kun acceptabelt lokalt.
/// </summary>
public sealed class DataProtectionMasterKey(byte[] key)
{
    public byte[] Key { get; } = key.Length == 32
        ? key
        : throw new ArgumentException("DATAPROTECTION_KEY skal være 32 bytes (base64), fx fra: openssl rand -base64 32");
}

/// <summary>Krypterer Data Protection-nøgler med AES-GCM inden de gemmes i databasen.</summary>
public sealed class AesXmlEncryptor(DataProtectionMasterKey masterKey) : IXmlEncryptor
{
    public EncryptedXmlInfo Encrypt(XElement plaintextElement)
    {
        var plaintext = Encoding.UTF8.GetBytes(plaintextElement.ToString(SaveOptions.DisableFormatting));
        var nonce = RandomNumberGenerator.GetBytes(AesGcm.NonceByteSizes.MaxSize);
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        var ciphertext = new byte[plaintext.Length];

        using (var aes = new AesGcm(masterKey.Key, tag.Length))
            aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var blob = Convert.ToBase64String([.. nonce, .. tag, .. ciphertext]);
        var element = new XElement("encryptedKey",
            new XComment(" Krypteret med AES-GCM (DATAPROTECTION_KEY). "),
            new XElement("value", blob));

        return new EncryptedXmlInfo(element, typeof(AesXmlDecryptor));
    }
}

/// <summary>Modstykket til <see cref="AesXmlEncryptor"/>. Instansieres af Data Protection via DI.</summary>
public sealed class AesXmlDecryptor(IServiceProvider services) : IXmlDecryptor
{
    public XElement Decrypt(XElement encryptedElement)
    {
        var key = services.GetRequiredService<DataProtectionMasterKey>().Key;
        var blob = Convert.FromBase64String(encryptedElement.Element("value")!.Value);

        var nonceLen = AesGcm.NonceByteSizes.MaxSize;
        var tagLen = AesGcm.TagByteSizes.MaxSize;
        var nonce = blob.AsSpan(0, nonceLen);
        var tag = blob.AsSpan(nonceLen, tagLen);
        var ciphertext = blob.AsSpan(nonceLen + tagLen);
        var plaintext = new byte[ciphertext.Length];

        using (var aes = new AesGcm(key, tagLen))
            aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return XElement.Parse(Encoding.UTF8.GetString(plaintext));
    }
}
