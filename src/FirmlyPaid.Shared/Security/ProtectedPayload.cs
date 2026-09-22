namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Ciphertext plus the key version it was written with, so keys can rotate without
/// making old rows unreadable. The nonce and authentication tag live inside
/// <paramref name="Ciphertext"/>; callers treat the whole thing as opaque bytes.
/// </summary>
public sealed record ProtectedPayload(byte[] Ciphertext, string KeyVersion)
{
    public override string ToString() =>
        // Never let a template or token reach a log through string interpolation.
        $"ProtectedPayload({Ciphertext.Length} bytes, key {KeyVersion})";
}
