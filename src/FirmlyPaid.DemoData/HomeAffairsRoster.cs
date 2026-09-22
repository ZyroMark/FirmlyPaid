using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;

namespace FirmlyPaid.DemoData;

/// <summary>
/// The 20 test identities the Home Affairs simulator answers for (part 7). The first ten
/// are the seeded customers, so anyone already in the database verifies cleanly. The
/// other ten exist to exercise the outcomes an agent has to handle: no match, deceased,
/// and the service being down.
/// </summary>
public static class HomeAffairsRoster
{
    /// <param name="First12Digits">The check digit is calculated, so every entry is a genuinely valid number.</param>
    public sealed record RosterEntry(string FullName, string First12Digits, HomeAffairsOutcome Outcome)
    {
        public string IdNumber => SouthAfricanIdNumber.WithCheckDigit(First12Digits);
    }

    /// <summary>The ten people who are also seeded customers. All verify as a match.</summary>
    private static readonly IReadOnlyList<RosterEntry> KnownCustomers =
        SeedCatalogue.People
            .Select(p => new RosterEntry(p.FullName, p.First12Digits, HomeAffairsOutcome.Match))
            .ToList();

    /// <summary>Ten more identities, each one there to force a particular outcome.</summary>
    private static readonly IReadOnlyList<RosterEntry> TestOutcomes =
    [
        new("Naledi Sithole", "960418112208", HomeAffairsOutcome.Match),
        new("Pieter Coetzee", "870925334408", HomeAffairsOutcome.Match),

        // The name on the application does not match the fingerprint on file.
        new("Kagiso Molefe", "890513556608", HomeAffairsOutcome.NoMatch),
        new("Elsie Jacobs", "750302778808", HomeAffairsOutcome.NoMatch),
        new("Thabo Mabaso", "920227990008", HomeAffairsOutcome.NoMatch),

        // Marked deceased on the population register. Enrolment must stop.
        new("Gerhard Pretorius", "660809221108", HomeAffairsOutcome.Deceased),
        new("Nokuthula Mthembu", "710114443308", HomeAffairsOutcome.Deceased),

        // Home Affairs is not answering. The agent is told to try again later.
        new("Shaun Daniels", "941030665508", HomeAffairsOutcome.ServiceUnavailable),
        new("Zanele Ngcobo", "830621887708", HomeAffairsOutcome.ServiceUnavailable),
        new("Imraan Patel", "781205009908", HomeAffairsOutcome.ServiceUnavailable),
    ];

    public static readonly IReadOnlyList<RosterEntry> All = [.. KnownCustomers, .. TestOutcomes];

    /// <summary>
    /// Looks up an ID number. An identity that is not on the roster answers NoMatch,
    /// which is what Home Affairs would say about someone who is not on the register.
    /// </summary>
    public static RosterEntry? Find(string idNumber) =>
        All.FirstOrDefault(entry => entry.IdNumber == idNumber);
}
