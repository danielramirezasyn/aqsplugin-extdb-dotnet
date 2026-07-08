using AqsPluginExtDb.Core.Crypto;
using Xunit;

namespace AqsPluginExtDb.Tests;

public class CryptoServiceTests
{
    [Fact]
    public void Encrypt_Then_Decrypt_ReturnsOriginalPlaintext()
    {
        var crypto = new CryptoService("correct-horse-battery-staple");

        string encrypted = crypto.Encrypt("s3cret-password");
        string decrypted = crypto.Decrypt(encrypted);

        Assert.Equal("s3cret-password", decrypted);
    }

    [Fact]
    public void Encrypt_ProducesDifferentCiphertextEachTime()
    {
        var crypto = new CryptoService("key");

        string first = crypto.Encrypt("same-value");
        string second = crypto.Encrypt("same-value");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void Encrypt_OutputStartsWithEncPrefix()
    {
        var crypto = new CryptoService("key");

        string encrypted = crypto.Encrypt("value");

        Assert.StartsWith("ENC:", encrypted, StringComparison.Ordinal);
    }

    [Fact]
    public void Decrypt_WithWrongKey_ThrowsCryptoException()
    {
        var crypto = new CryptoService("right-key");
        string encrypted = crypto.Encrypt("value");

        var wrongKeyCrypto = new CryptoService("wrong-key");

        Assert.Throws<CryptoException>(() => wrongKeyCrypto.Decrypt(encrypted));
    }

    [Fact]
    public void Decrypt_WithTamperedCiphertext_ThrowsCryptoException()
    {
        var crypto = new CryptoService("key");
        string encrypted = crypto.Encrypt("a reasonably long secret value");

        char[] chars = encrypted.ToCharArray();
        int flipIndex = chars.Length - 5;
        chars[flipIndex] = chars[flipIndex] == 'A' ? 'B' : 'A';
        string tampered = new(chars);

        Assert.Throws<CryptoException>(() => crypto.Decrypt(tampered));
    }

    [Fact]
    public void Decrypt_WithUnrecognizedFormat_ThrowsCryptoException()
    {
        var crypto = new CryptoService("key");

        Assert.Throws<CryptoException>(() => crypto.Decrypt("plaintext-not-encrypted"));
    }

    [Fact]
    public void Decrypt_WithInvalidBase64_ThrowsCryptoException()
    {
        var crypto = new CryptoService("key");

        Assert.Throws<CryptoException>(() => crypto.Decrypt("ENC:not-valid-base64!!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("a very long password with spaces and Ñ, é, 中文 characters!")]
    public void Encrypt_Then_Decrypt_RoundtripsVariousInputs(string plaintext)
    {
        var crypto = new CryptoService("key-for-roundtrip");

        string decrypted = crypto.Decrypt(crypto.Encrypt(plaintext));

        Assert.Equal(plaintext, decrypted);
    }
}
