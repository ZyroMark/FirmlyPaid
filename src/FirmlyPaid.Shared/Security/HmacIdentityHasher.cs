using System.Security.Cryptography;
using System.Text;
using FirmlyPaid.Shared.Abstractions;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// Hashes an ID number with a server-held pepper (rule 10.4). Keyed and deterministic,
/// so enrolment can still spot the same person signing up twice, but the hash is useless
/// to anyone who steals the database without the pepper.
/// </summary>
/// <remarks>
/// Deliberately not a slow hash. A 13 digit ID number has too small a space for salting
/// alone to help, so the protection comes from the pepper being kept out of the database
/// entirely. PINs are a different matter and use Argon2id (see IPinHasher).
/// </remarks>
public sealed class HmacIdentityHasher : IIdentityHasher
{
    private readonly byte[] _pepper;

    public HmacIdentityHasher(string pepper)
    {
        if (string.IsNullOrWhiteSpace(pepper) || pepper.Length < 32)
        {
            throw new ArgumentException(
                "The ID number pepper must be at least 32 characters. Set it from the environment, never in source.",
                nameof(pepper));
        }

        _pepper = Encoding.UTF8.GetBytes(pepper);
    }

    public string HashIdNumber(string idNumber)
    {
        if (!SouthAfricanIdNumber.IsValid(idNumber))
        {
            throw new ArgumentException("ID number is not valid.", nameof(idNumber));
        }

        var hash = HMACSHA512.HashData(_pepper, Encoding.UTF8.GetBytes(idNumber));

        // Truncated to 32 bytes: still far beyond collision range, and fits the column.
        return Convert.ToHexStringLower(hash.AsSpan(0, 32));
    }
}
