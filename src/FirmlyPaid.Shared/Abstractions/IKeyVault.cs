using FirmlyPaid.Shared.Security;

namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Source of the keys that protect biometric templates, account tokens and PIN transport.
/// Local builds use a protected dev key file; production swaps in AWS KMS, then CloudHSM.
/// </summary>
public interface IKeyVault
{
    /// <summary>The key version new ciphertext is written with, so old data stays readable after rotation.</summary>
    string CurrentKeyVersion { get; }

    Task<ProtectedPayload> ProtectAsync(ReadOnlyMemory<byte> plaintext, CancellationToken ct = default);

    /// <summary>Decrypts a payload using the key version it was written with.</summary>
    Task<byte[]> UnprotectAsync(ProtectedPayload payload, CancellationToken ct = default);
}
