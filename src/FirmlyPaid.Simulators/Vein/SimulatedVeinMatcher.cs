using FirmlyPaid.Shared.Abstractions;

namespace FirmlyPaid.Simulators.Vein;

/// <summary>
/// Stands in for the Hitachi matching engine. Scores how alike two decrypted templates are,
/// on the same 0 to 1 scale a real engine reports, so the threshold in configuration keeps
/// its meaning when the real engine arrives.
/// </summary>
/// <remarks>
/// The score is one minus the average per-byte difference. Two reads of the same finger
/// differ only by sensor noise and land near 1.0; two different fingers are uncorrelated
/// byte patterns and land near 0.67, well below the 0.85 threshold.
/// </remarks>
public sealed class SimulatedVeinMatcher : IVeinMatcher
{
    public double Compare(ReadOnlySpan<byte> probe, ReadOnlySpan<byte> candidate)
    {
        // Different lengths mean different template formats, never the same finger.
        if (probe.Length == 0 || probe.Length != candidate.Length)
        {
            return 0.0;
        }

        long totalDifference = 0;

        for (var i = 0; i < probe.Length; i++)
        {
            totalDifference += Math.Abs(probe[i] - candidate[i]);
        }

        var averageDifference = (double)totalDifference / probe.Length;

        return 1.0 - (averageDifference / 255.0);
    }
}
