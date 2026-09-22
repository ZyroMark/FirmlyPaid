using System.Security.Cryptography;
using System.Text;

namespace FirmlyPaid.Shared.Security;

/// <summary>
/// The hash chain behind the append-only audit log. Each row covers its own content plus
/// the previous row's hash, so editing or removing any row breaks every hash after it
/// (FR-13). Both the writer and the admin console's verifier use this one method.
/// </summary>
public static class AuditHash
{
    /// <summary>The hash the very first row chains from.</summary>
    public const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>Unit separator. Chosen because it cannot appear in any of the fields.</summary>
    private const char FieldSeparator = (char)0x1f;

    public static string Compute(
        string previousHash,
        string actor,
        string action,
        string entityType,
        string entityId,
        string details,
        DateTime createdAtUtc)
    {
        // A fixed field order and separator: the same row must always hash the same way.
        var canonical = string.Join(
            FieldSeparator,
            previousHash,
            actor,
            action,
            entityType,
            entityId,
            details,
            createdAtUtc.ToString("O"));

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
