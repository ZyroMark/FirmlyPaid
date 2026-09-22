namespace FirmlyPaid.Shared.Contracts;

/// <summary>
/// How already-encrypted bytes travel over HTTP: base64 ciphertext plus the key version
/// it was written with. Used for vein templates and PINs, never for images.
/// </summary>
public sealed record EncryptedPayloadDto(string CiphertextBase64, string KeyVersion);
