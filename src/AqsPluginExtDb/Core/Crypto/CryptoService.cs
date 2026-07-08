using System.Security.Cryptography;
using System.Text;

namespace AqsPluginExtDb.Core.Crypto;

/// <summary>
/// Encrypts/decrypts secrets at rest using AES-256-GCM with a per-value random salt,
/// deriving the key from ENCRYPTION_KEY via PBKDF2-HMAC-SHA256.
/// </summary>
public sealed class CryptoService(string encryptionKey)
{
    private const string Prefix = "ENC:";
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public string Encrypt(string plaintext)
    {
        Span<byte> salt = stackalloc byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        byte[] key = DeriveKey(salt);

        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        byte[] plainBytes = Encoding.UTF8.GetBytes(plaintext);
        byte[] cipherBytes = new byte[plainBytes.Length];
        Span<byte> tag = stackalloc byte[TagSize];

        using (var aesGcm = new AesGcm(key, TagSize))
        {
            aesGcm.Encrypt(nonce, plainBytes, cipherBytes, tag);
        }

        byte[] payload = new byte[SaltSize + NonceSize + cipherBytes.Length + TagSize];
        salt.CopyTo(payload);
        nonce.CopyTo(payload.AsSpan(SaltSize));
        cipherBytes.CopyTo(payload.AsSpan(SaltSize + NonceSize));
        tag.CopyTo(payload.AsSpan(SaltSize + NonceSize + cipherBytes.Length));

        return Prefix + Convert.ToBase64String(payload);
    }

    public string Decrypt(string stored)
    {
        if (!stored.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new CryptoException("Unrecognized ciphertext format.");
        }

        byte[] payload;
        try
        {
            payload = Convert.FromBase64String(stored[Prefix.Length..]);
        }
        catch (FormatException ex)
        {
            throw new CryptoException("Ciphertext is not valid base64.", ex);
        }

        if (payload.Length < SaltSize + NonceSize + TagSize)
        {
            throw new CryptoException("Ciphertext payload is too short.");
        }

        var salt = payload.AsSpan(0, SaltSize);
        var nonce = payload.AsSpan(SaltSize, NonceSize);
        int cipherLength = payload.Length - SaltSize - NonceSize - TagSize;
        var cipherBytes = payload.AsSpan(SaltSize + NonceSize, cipherLength);
        var tag = payload.AsSpan(SaltSize + NonceSize + cipherLength, TagSize);

        byte[] key = DeriveKey(salt);
        byte[] plainBytes = new byte[cipherLength];

        try
        {
            using var aesGcm = new AesGcm(key, TagSize);
            aesGcm.Decrypt(nonce, cipherBytes, tag, plainBytes);
        }
        catch (CryptographicException ex)
        {
            throw new CryptoException("Failed to decrypt value: authentication tag mismatch or wrong key.", ex);
        }

        return Encoding.UTF8.GetString(plainBytes);
    }

    private byte[] DeriveKey(ReadOnlySpan<byte> salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(encryptionKey), salt, Iterations, HashAlgorithmName.SHA256, KeySize);
}

public sealed class CryptoException : Exception
{
    public CryptoException(string message) : base(message)
    {
    }

    public CryptoException(string message, Exception inner) : base(message, inner)
    {
    }
}
