using FirmlyPaid.Shared.Contracts;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Moves already-encrypted bytes between the in-process type and the wire type.
/// Neither direction ever sees plaintext, so this is safe to use anywhere.
/// </summary>
public static class EncryptedPayloadConversion
{
    public static EncryptedPayloadDto ToDto(this ProtectedPayload payload) =>
        new(Convert.ToBase64String(payload.Ciphertext), payload.KeyVersion);

    public static ProtectedPayload ToPayload(this EncryptedPayloadDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.KeyVersion))
        {
            throw Errors.FirmlyPaidException.Validation("The encrypted payload is missing its key version.");
        }

        byte[] ciphertext;

        try
        {
            ciphertext = Convert.FromBase64String(dto.CiphertextBase64);
        }
        catch (FormatException)
        {
            throw Errors.FirmlyPaidException.Validation("The encrypted payload is not valid base64.");
        }

        return ciphertext.Length == 0
            ? throw Errors.FirmlyPaidException.Validation("The encrypted payload is empty.")
            : new ProtectedPayload(ciphertext, dto.KeyVersion);
    }
}
