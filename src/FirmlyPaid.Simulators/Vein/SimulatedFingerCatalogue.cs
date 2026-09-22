using System.Security.Cryptography;
using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Enums;

namespace FirmlyPaid.Simulators.Vein;

/// <summary>
/// The list of test fingers the simulated scanner panel offers. Each one is a fixed byte
/// pattern derived from its label and finger position, so the same test finger reads the
/// same way in every process and on every machine, and two different fingers never collide.
/// </summary>
public static class SimulatedFingerCatalogue
{
    /// <summary>How many bytes a template is. Enough for two fingers to be clearly apart.</summary>
    public const int TemplateLength = 256;

    /// <summary>The two fingers an agent captures at enrolment.</summary>
    public static readonly IReadOnlyList<FingerPosition> EnrolledPositions =
        [FingerPosition.RightIndex, FingerPosition.LeftIndex];

    public sealed record SimulatedFinger(string Label, FingerPosition Position)
    {
        /// <summary>What the panel shows the operator, for example "Thandiwe Mokoena, right index".</summary>
        public string DisplayName => $"{Label}, {Describe(Position)}";
    }

    /// <summary>
    /// One entry per seeded customer per enrolled finger: 20 fingers in all. Named after
    /// the demo cast so a demonstration reads naturally.
    /// </summary>
    public static readonly IReadOnlyList<SimulatedFinger> All =
    [
        .. SeedCatalogue.People.SelectMany(person =>
            EnrolledPositions.Select(position => new SimulatedFinger(person.FullName, position)))
    ];

    /// <summary>
    /// The stored pattern for a finger. Derived by hashing the label and position, so it
    /// is stable across runs without a lookup table, and unrelated fingers look unrelated.
    /// </summary>
    public static byte[] PatternFor(string label, FingerPosition position)
    {
        var template = new byte[TemplateLength];
        var seed = System.Text.Encoding.UTF8.GetBytes($"firmlypaid-simulated-finger:{label}:{position}");

        // Hash chaining fills the template deterministically without a fixed key.
        var block = SHA512.HashData(seed);
        var written = 0;

        while (written < TemplateLength)
        {
            var take = Math.Min(block.Length, TemplateLength - written);
            block.AsSpan(0, take).CopyTo(template.AsSpan(written));
            written += take;
            block = SHA512.HashData(block);
        }

        return template;
    }

    public static byte[] PatternFor(SimulatedFinger finger) => PatternFor(finger.Label, finger.Position);

    private static string Describe(FingerPosition position) => position switch
    {
        FingerPosition.LeftThumb => "left thumb",
        FingerPosition.LeftIndex => "left index",
        FingerPosition.LeftMiddle => "left middle",
        FingerPosition.RightThumb => "right thumb",
        FingerPosition.RightIndex => "right index",
        FingerPosition.RightMiddle => "right middle",
        _ => "unknown finger",
    };
}
