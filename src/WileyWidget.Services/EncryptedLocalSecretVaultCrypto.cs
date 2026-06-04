using System;
using System.Security.Cryptography;

namespace WileyWidget.Services;

/// <summary>
/// Cross-platform protect/unprotect for <see cref="EncryptedLocalSecretVaultService"/>.
/// Windows uses DPAPI; Linux/macOS use AES-256-GCM with vault entropy (CI Playwright, Docker).
/// </summary>
internal static class EncryptedLocalSecretVaultCrypto
{
    internal const string AesEntropyFilePrefix = "AESFN1:";

    internal static bool UseDpapi => OperatingSystem.IsWindows();

    internal static byte[] Protect(byte[] plainBytes, byte[] entropy, DataProtectionScope scope)
    {
        if (UseDpapi)
        {
            return ProtectedData.Protect(plainBytes, entropy, scope);
        }

        return ProtectAesGcm(plainBytes, entropy);
    }

    internal static byte[] Unprotect(byte[] protectedBytes, byte[] entropy, DataProtectionScope scope)
    {
        if (UseDpapi)
        {
            return ProtectedData.Unprotect(protectedBytes, entropy, scope);
        }

        return UnprotectAesGcm(protectedBytes, entropy);
    }

    internal static byte[] ProtectEntropyForStorage(byte[] entropy)
    {
        if (UseDpapi)
        {
            return ProtectedData.Protect(entropy, null, DataProtectionScope.LocalMachine);
        }

        return entropy;
    }

    internal static byte[] UnprotectEntropyFromStorage(byte[] stored)
    {
        if (UseDpapi)
        {
            return ProtectedData.Unprotect(stored, null, DataProtectionScope.LocalMachine);
        }

        return stored;
    }

    internal static bool TryReadAesEntropyFile(string content, out byte[] entropy)
    {
        entropy = Array.Empty<byte>();
        if (!content.StartsWith(AesEntropyFilePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        var payload = content[AesEntropyFilePrefix.Length..].Trim();
        if (string.IsNullOrEmpty(payload))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(payload);
            if (bytes.Length != 32)
            {
                return false;
            }

            entropy = bytes;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static string FormatAesEntropyFile(byte[] entropy)
    {
        return AesEntropyFilePrefix + Convert.ToBase64String(entropy);
    }

    private static byte[] ProtectAesGcm(byte[] plainBytes, byte[] entropy)
    {
        var key = DeriveAesKey(entropy);
        var nonce = new byte[AesGcm.NonceByteSizes.MaxSize];
        RandomNumberGenerator.Fill(nonce);

        var cipher = new byte[plainBytes.Length];
        var tag = new byte[AesGcm.TagByteSizes.MaxSize];
        using var aes = new AesGcm(key, AesGcm.TagByteSizes.MaxSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var combined = new byte[nonce.Length + cipher.Length + tag.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(cipher, 0, combined, nonce.Length, cipher.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length + cipher.Length, tag.Length);
        return combined;
    }

    private static byte[] UnprotectAesGcm(byte[] protectedBytes, byte[] entropy)
    {
        const int nonceSize = 12;
        const int tagSize = 16;
        if (protectedBytes.Length < nonceSize + tagSize)
        {
            throw new CryptographicException("Invalid AES-GCM protected payload.");
        }

        var key = DeriveAesKey(entropy);
        var nonce = new byte[nonceSize];
        var tag = new byte[tagSize];
        var cipherLength = protectedBytes.Length - nonceSize - tagSize;
        var cipher = new byte[cipherLength];

        Buffer.BlockCopy(protectedBytes, 0, nonce, 0, nonceSize);
        Buffer.BlockCopy(protectedBytes, nonceSize, cipher, 0, cipherLength);
        Buffer.BlockCopy(protectedBytes, nonceSize + cipherLength, tag, 0, tagSize);

        var plain = new byte[cipherLength];
        using var aes = new AesGcm(key, tagSize);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static byte[] DeriveAesKey(byte[] entropy)
    {
        return SHA256.HashData(entropy);
    }
}
