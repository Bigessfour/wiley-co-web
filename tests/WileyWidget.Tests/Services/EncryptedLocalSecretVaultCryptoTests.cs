using System.Security.Cryptography;
using System.Text;
using WileyWidget.Services;
using Xunit;

namespace WileyWidget.Tests.Services;

public sealed class EncryptedLocalSecretVaultCryptoTests
{
    [Fact]
    public void Protect_Unprotect_RoundTrips_OnCurrentPlatform()
    {
        var entropy = RandomNumberGenerator.GetBytes(32);
        var plain = Encoding.UTF8.GetBytes("ci-playwright-stub-key");
        // Scope is only used on Windows (DPAPI); AES-GCM path ignores it on Linux CI.
        var scope = default(DataProtectionScope);

        var protectedBytes = EncryptedLocalSecretVaultCrypto.Protect(plain, entropy, scope);
        var roundTrip = EncryptedLocalSecretVaultCrypto.Unprotect(protectedBytes, entropy, scope);

        Assert.Equal(plain, roundTrip);
    }

    [Fact]
    public void AesEntropyFileFormat_RoundTrips()
    {
        var entropy = RandomNumberGenerator.GetBytes(32);
        var formatted = EncryptedLocalSecretVaultCrypto.FormatAesEntropyFile(entropy);

        Assert.True(
            EncryptedLocalSecretVaultCrypto.TryReadAesEntropyFile(formatted, out var loaded));
        Assert.Equal(entropy, loaded);
    }
}
