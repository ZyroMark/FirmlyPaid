using System.Security.Cryptography;
using System.Text;
using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Signs and checks the messages the sponsor bank sends us. A confirmation callback says
/// "this customer owns this account", so anyone who could forge one could attach someone
/// else's bank account to their own profile.
/// </summary>
/// <remarks>
/// A shared secret with an HMAC is what the pilot bank supports. The secret comes from the
/// environment, never from source (rule 10.15). When the sponsor bank offers signed
/// requests with its own certificate, only this class changes.
/// </remarks>
public sealed class BankCallbackSignature
{
    private readonly byte[] _secret;

    public BankCallbackSignature(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            throw new ArgumentException(
                "The bank callback secret must be at least 32 characters. Set it from the environment.",
                nameof(secret));
        }

        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string Sign(string confirmationReference, AccountConfirmationOutcome outcome)
    {
        var message = Encoding.UTF8.GetBytes($"{confirmationReference}|{outcome}");

        return Convert.ToHexStringLower(HMACSHA256.HashData(_secret, message));
    }

    /// <summary>
    /// Compares in fixed time. A signature check that returns early on the first wrong
    /// character tells an attacker how much of their guess was right.
    /// </summary>
    public bool IsValid(string confirmationReference, AccountConfirmationOutcome outcome, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var expected = Encoding.UTF8.GetBytes(Sign(confirmationReference, outcome));
        var supplied = Encoding.UTF8.GetBytes(signature);

        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
