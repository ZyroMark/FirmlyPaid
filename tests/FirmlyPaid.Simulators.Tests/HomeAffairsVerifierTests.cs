using FirmlyPaid.DemoData;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators.Fingerprint;
using FirmlyPaid.Simulators.HomeAffairs;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// FR-03: the four outcomes an agent has to handle, and the rule that a number with a bad
/// checksum is caught before the partner is ever called.
/// </summary>
public class HomeAffairsVerifierTests
{
    private readonly SimulatorControlState _control = new();
    private readonly SimulatedHomeAffairsVerifier _verifier;

    public HomeAffairsVerifierTests()
    {
        _verifier = new SimulatedHomeAffairsVerifier(_control, NullLogger<SimulatedHomeAffairsVerifier>.Instance);
    }

    [Theory]
    [InlineData(HomeAffairsOutcome.Match)]
    [InlineData(HomeAffairsOutcome.NoMatch)]
    [InlineData(HomeAffairsOutcome.Deceased)]
    [InlineData(HomeAffairsOutcome.ServiceUnavailable)]
    public async Task TheRosterCoversEveryOutcome(HomeAffairsOutcome expected)
    {
        var entry = HomeAffairsRoster.All.First(e => e.Outcome == expected);

        var result = await VerifyAsync(entry.IdNumber, entry.FullName);

        result.Outcome.Should().Be(expected);
        result.Reference.Should().StartWith("HA-SIM-");
    }

    [Fact]
    public void TheRosterHasTwentyIdentitiesAndEveryNumberIsValid()
    {
        HomeAffairsRoster.All.Should().HaveCount(20);

        foreach (var entry in HomeAffairsRoster.All)
        {
            SouthAfricanIdNumber.IsValid(entry.IdNumber)
                .Should().BeTrue($"{entry.FullName} must have a valid ID number");
        }

        HomeAffairsRoster.All.Select(e => e.IdNumber).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task EverySeededCustomerVerifiesAsAMatch()
    {
        // Anyone already in the database must pass, or a demo enrolment contradicts the
        // data that is already there.
        foreach (var person in SeedCatalogue.People)
        {
            var result = await VerifyAsync(person.IdNumber, person.FullName);
            result.Outcome.Should().Be(HomeAffairsOutcome.Match, $"{person.FullName} is a seeded customer");
        }
    }

    [Fact]
    public async Task AnIdNumberWithABadChecksumIsRefusedBeforeTheCallIsMade()
    {
        // FR-03: checked before calling. The real partner charges per query.
        var act = async () => await VerifyAsync("8001015009088", "Someone");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*IsValid*");
    }

    [Fact]
    public async Task SomeoneNotOnTheRegisterIsANoMatch()
    {
        var unknown = SouthAfricanIdNumber.WithCheckDigit("651231777708");

        var result = await VerifyAsync(unknown, "Not On The Roster");

        result.Outcome.Should().Be(HomeAffairsOutcome.NoMatch);
    }

    [Fact]
    public async Task TheDemoSwitchboardCanForceAnyOutcome()
    {
        // Part 7: an admin page switches simulator outcomes live during a demonstration.
        var person = SeedCatalogue.People[0];
        _control.HomeAffairsOverride = HomeAffairsOutcome.ServiceUnavailable;

        var result = await VerifyAsync(person.IdNumber, person.FullName);

        result.Outcome.Should().Be(HomeAffairsOutcome.ServiceUnavailable);
    }

    [Fact]
    public async Task TheReferenceIsStablePerPersonAndCarriesNoIdNumber()
    {
        var person = SeedCatalogue.People[0];

        var first = await VerifyAsync(person.IdNumber, person.FullName);
        var second = await VerifyAsync(person.IdNumber, person.FullName);

        second.Reference.Should().Be(first.Reference);

        // Rule 10.5: the reference is stored and audited, so it must be safe to write down.
        SensitiveDataPatterns.FindViolation(first.Reference).Should().BeNull();
        first.Reference.Should().NotContain(person.IdNumber);
    }

    [Fact]
    public async Task AVerificationWithoutAFingerprintIsRefused()
    {
        var person = SeedCatalogue.People[0];

        var act = async () => await _verifier.VerifyAsync(
            new HomeAffairsVerificationRequest(person.IdNumber, person.FullName, []));

        await act.Should().ThrowAsync<ArgumentException>();
    }

    private Task<HomeAffairsVerificationResult> VerifyAsync(string idNumber, string fullName) =>
        _verifier.VerifyAsync(new HomeAffairsVerificationRequest(
            idNumber,
            fullName,
            SimulatedFingerprintScanner.SampleFor(fullName)));
}
