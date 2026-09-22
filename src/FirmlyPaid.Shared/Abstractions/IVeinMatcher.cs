namespace FirmlyPaid.Shared.Abstractions;

/// <summary>
/// Compares two decrypted vein templates. Only the Matching service ever calls this,
/// because only it may decrypt vault data (rule 10.2).
/// </summary>
public interface IVeinMatcher
{
    /// <summary>Similarity from 0.0 (nothing alike) to 1.0 (identical).</summary>
    double Compare(ReadOnlySpan<byte> probe, ReadOnlySpan<byte> candidate);
}
