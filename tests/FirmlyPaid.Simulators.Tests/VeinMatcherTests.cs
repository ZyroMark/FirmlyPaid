using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Simulators.Vein;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// The matcher has to separate the same finger read twice from two different fingers, with
/// enough room either side of the configured threshold that sensor noise does not decide
/// the outcome. FR-06 and FR-07 both rest on this.
/// </summary>
public class VeinMatcherTests : IDisposable
{
    private readonly SimulatorTestHost _host = new();
    private readonly SimulatedVeinMatcher _matcher = new();
    private readonly SimulatedVeinScanner _scanner;
    private readonly double _threshold = new MatchingOptions().MatchThreshold;

    public VeinMatcherTests()
    {
        _scanner = new SimulatedVeinScanner(_host.Control, _host.KeyVault, NullLogger<SimulatedVeinScanner>.Instance);
        _host.Control.VeinScanner = VeinScannerMode.GoodRead;
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task TheSameFingerReadTwiceScoresAboveTheThreshold()
    {
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];

        var enrolled = await ReadAsync();
        var atTheTill = await ReadAsync();

        _matcher.Compare(enrolled, atTheTill).Should().BeGreaterThan(_threshold);
    }

    [Fact]
    public async Task ADifferentPersonsFingerScoresWellBelowTheThreshold()
    {
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];
        var enrolled = await ReadAsync();

        _scanner.SelectedFinger = SimulatedFingerCatalogue.All.First(f => f.Label != SimulatedFingerCatalogue.All[0].Label);
        var someoneElse = await ReadAsync();

        _matcher.Compare(enrolled, someoneElse).Should().BeLessThan(_threshold);
    }

    [Fact]
    public async Task TheOtherFingerOfTheSamePersonIsNotAMatch()
    {
        // Two fingers are enrolled per customer, and they must stay apart: the left index
        // is a separate credential, not a fuzzy version of the right.
        var person = SimulatedFingerCatalogue.All[0].Label;

        _scanner.SelectedFinger = new SimulatedFingerCatalogue.SimulatedFinger(person, FingerPosition.RightIndex);
        var right = await ReadAsync(FingerPosition.RightIndex);

        _scanner.SelectedFinger = new SimulatedFingerCatalogue.SimulatedFinger(person, FingerPosition.LeftIndex);
        var left = await ReadAsync(FingerPosition.LeftIndex);

        _matcher.Compare(right, left).Should().BeLessThan(_threshold);
    }

    [Fact]
    public void AnIdenticalTemplateScoresOne()
    {
        var template = SimulatedFingerCatalogue.PatternFor("Thandiwe Mokoena", FingerPosition.RightIndex);

        _matcher.Compare(template, template).Should().Be(1.0);
    }

    [Fact]
    public void TemplatesOfDifferentLengthsNeverMatch()
    {
        _matcher.Compare(new byte[] { 1, 2, 3 }, new byte[] { 1, 2, 3, 4 }).Should().Be(0.0);
        _matcher.Compare([], []).Should().Be(0.0);
    }

    [Fact]
    public async Task ANoisyReadEventuallyFallsBelowTheThreshold()
    {
        // The demo switchboard can wind the noise up to show a genuine near miss rather
        // than a contrived one.
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];
        var enrolled = await ReadAsync();

        _host.Control.VeinNoiseAmplitude = 120;
        var noisy = await ReadAsync();

        _matcher.Compare(enrolled, noisy).Should().BeLessThan(_threshold);
    }

    [Fact]
    public async Task MatchingStaysComfortablyClearOfTheThresholdAcrossManyReads()
    {
        // Run the same finger repeatedly: a flaky separation here would show up at a till
        // as an occasional unexplained NO_MATCH.
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];
        var enrolled = await ReadAsync();

        var scores = new List<double>();
        for (var i = 0; i < 50; i++)
        {
            var atTheTill = await ReadAsync();
            scores.Add(_matcher.Compare(enrolled, atTheTill));
        }

        scores.Min().Should().BeGreaterThan(_threshold + 0.05);
    }

    private async Task<byte[]> ReadAsync(FingerPosition position = FingerPosition.RightIndex)
    {
        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(position));
        return await _host.KeyVault.UnprotectAsync(result.EncryptedTemplate!);
    }
}
