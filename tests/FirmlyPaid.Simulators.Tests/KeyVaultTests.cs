using System.Security.Cryptography;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators.Keys;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// The key vault protects every template and account token, so its failure modes matter
/// more than its happy path. Tampered ciphertext must fail loudly, not decrypt to rubbish.
/// </summary>
public class KeyVaultTests : IDisposable
{
    private readonly string _keyFilePath = Path.Combine(Path.GetTempPath(), $"firmlypaid-keytest-{Guid.NewGuid():N}.devkey");

    public void Dispose()
    {
        if (File.Exists(_keyFilePath))
        {
            File.Delete(_keyFilePath);
        }
    }

    [Fact]
    public async Task SomethingEncryptedComesBackUnchanged()
    {
        var vault = CreateVault();
        var template = RandomNumberGenerator.GetBytes(256);

        var protectedPayload = await vault.ProtectAsync(template);
        var recovered = await vault.UnprotectAsync(protectedPayload);

        recovered.Should().Equal(template);
    }

    [Fact]
    public async Task TheCiphertextLooksNothingLikeThePlaintext()
    {
        var vault = CreateVault();
        var template = new byte[256];

        var protectedPayload = await vault.ProtectAsync(template);

        protectedPayload.Ciphertext.Should().NotEqual(template);
        protectedPayload.KeyVersion.Should().Be("dev-1");
    }

    [Fact]
    public async Task EncryptingTheSameBytesTwiceGivesDifferentCiphertext()
    {
        // A fresh nonce each time. Otherwise two customers with the same template would
        // be visibly the same in the vault.
        var vault = CreateVault();
        var template = RandomNumberGenerator.GetBytes(256);

        var first = await vault.ProtectAsync(template);
        var second = await vault.ProtectAsync(template);

        first.Ciphertext.Should().NotEqual(second.Ciphertext);
    }

    [Fact]
    public async Task AlteredCiphertextIsRefusedRatherThanDecrypted()
    {
        // AES-GCM authenticates as well as encrypts, so a row edited in the database
        // fails to open instead of quietly producing a different template.
        var vault = CreateVault();
        var protectedPayload = await vault.ProtectAsync(RandomNumberGenerator.GetBytes(256));

        protectedPayload.Ciphertext[^1] ^= 0xFF;

        var act = async () => await vault.UnprotectAsync(protectedPayload);

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public async Task AnUnknownKeyVersionIsAFaultNotASilentFailure()
    {
        var vault = CreateVault();
        var protectedPayload = await vault.ProtectAsync(RandomNumberGenerator.GetBytes(64));

        var act = async () => await vault.UnprotectAsync(
            new ProtectedPayload(protectedPayload.Ciphertext, "dev-99"));

        await act.Should().ThrowAsync<CryptographicException>().WithMessage("*dev-99*");
    }

    [Fact]
    public async Task TheKeyFileSurvivesARestart()
    {
        // A new process must still read what the last one wrote, or every stored template
        // becomes unreadable on deployment.
        var first = CreateVault();
        var protectedPayload = await first.ProtectAsync(new byte[] { 1, 2, 3, 4 });

        var second = CreateVault();
        var recovered = await second.UnprotectAsync(protectedPayload);

        recovered.Should().Equal([1, 2, 3, 4]);
    }

    [Fact]
    public async Task AnEmptyPayloadIsRejected()
    {
        var vault = CreateVault();

        var act = async () => await vault.UnprotectAsync(new ProtectedPayload([], "dev-1"));

        await act.Should().ThrowAsync<CryptographicException>();
    }

    [Fact]
    public void TheKeyFileIsCreatedOnFirstUse()
    {
        File.Exists(_keyFilePath).Should().BeFalse();

        CreateVault();

        File.Exists(_keyFilePath).Should().BeTrue();
    }

    private LocalDevFileKeyVault CreateVault() =>
        new(_keyFilePath, NullLogger<LocalDevFileKeyVault>.Instance);
}
