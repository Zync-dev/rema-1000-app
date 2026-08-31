using System.Security.Cryptography;
using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using Rema.App.Services;

namespace Rema.App.Tests;

public class AesXmlEncryptorTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private static AesXmlDecryptor DecryptorWith(byte[] key)
    {
        var sp = new ServiceCollection()
            .AddSingleton(new DataProtectionMasterKey(key))
            .BuildServiceProvider();
        return new AesXmlDecryptor(sp);
    }

    [Fact]
    public void Round_trips_an_xml_key_element()
    {
        var original = XElement.Parse("<key id=\"abc\"><secret>super hemmelig værdi æøå</secret></key>");

        var encrypted = new AesXmlEncryptor(new DataProtectionMasterKey(Key)).Encrypt(original);
        var decrypted = DecryptorWith(Key).Decrypt(encrypted.EncryptedElement);

        Assert.Equal(original.ToString(SaveOptions.DisableFormatting),
                     decrypted.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void Ciphertext_does_not_contain_the_plaintext()
    {
        var original = XElement.Parse("<key><secret>PLAINTEXT-MARKER</secret></key>");

        var encrypted = new AesXmlEncryptor(new DataProtectionMasterKey(Key)).Encrypt(original);

        Assert.DoesNotContain("PLAINTEXT-MARKER", encrypted.EncryptedElement.ToString());
        Assert.Equal(typeof(AesXmlDecryptor), encrypted.DecryptorType);
    }

    [Fact]
    public void Wrong_key_fails_to_decrypt()
    {
        var original = XElement.Parse("<key><secret>x</secret></key>");
        var encrypted = new AesXmlEncryptor(new DataProtectionMasterKey(Key)).Encrypt(original);

        Assert.ThrowsAny<CryptographicException>(
            () => DecryptorWith(RandomNumberGenerator.GetBytes(32)).Decrypt(encrypted.EncryptedElement));
    }

    [Fact]
    public void Master_key_rejects_wrong_length()
    {
        Assert.Throws<ArgumentException>(() => new DataProtectionMasterKey(new byte[16]));
    }
}
