using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Simulators.Vein;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// One test per button on the simulated scanner panel (part 7), plus the behaviour that
/// matters most: no two reads are identical, so matching cannot be a byte comparison.
/// </summary>
public class VeinScannerTests : IDisposable
{
    private readonly SimulatorTestHost _host = new();
    private readonly SimulatedVeinScanner _scanner;

    public VeinScannerTests()
    {
        _scanner = new SimulatedVeinScanner(_host.Control, _host.KeyVault, NullLogger<SimulatedVeinScanner>.Instance);
    }

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task GoodRead_ReturnsAnEncryptedTemplateAndAHighQualityScore()
    {
        _host.Control.VeinScanner = VeinScannerMode.GoodRead;

        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));

        result.Outcome.Should().Be(VeinCaptureOutcome.Good);
        result.IsUsable.Should().BeTrue();
        result.QualityScore.Should().BeGreaterThan(60);
        result.EncryptedTemplate.Should().NotBeNull();
        result.EncryptedTemplate!.KeyVersion.Should().Be(_host.KeyVault.CurrentKeyVersion);
    }

    [Fact]
    public async Task PoorQuality_IsReturnedWithAScoreBelowTheEnrolmentThreshold()
    {
        // FR-01: a poor sample is rejected with a retry message rather than stored.
        _host.Control.VeinScanner = VeinScannerMode.PoorQuality;

        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));

        result.Outcome.Should().Be(VeinCaptureOutcome.PoorQuality);
        result.IsUsable.Should().BeFalse("a poor read must not count towards enrolment");
        result.QualityScore.Should().BeLessThan(60);
    }

    [Fact]
    public async Task FakeFinger_FailsLivenessAndProducesNoTemplateAtAll()
    {
        // FR-07: a printed or moulded finger must never reach the matcher.
        _host.Control.VeinScanner = VeinScannerMode.FakeFinger;

        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));

        result.Outcome.Should().Be(VeinCaptureOutcome.LivenessFailed);
        result.EncryptedTemplate.Should().BeNull();
        result.IsUsable.Should().BeFalse();
    }

    [Fact]
    public async Task NoFinger_ReturnsNothing()
    {
        _host.Control.VeinScanner = VeinScannerMode.NoFinger;

        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));

        result.Outcome.Should().Be(VeinCaptureOutcome.NoFinger);
        result.EncryptedTemplate.Should().BeNull();
    }

    [Fact]
    public async Task TwoReadsOfTheSameFingerAreNeverIdentical()
    {
        // If they were, matching would pass by accident on a byte comparison and the real
        // sensor would break the whole flow on day one.
        _host.Control.VeinScanner = VeinScannerMode.GoodRead;
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];

        var first = await CaptureTemplateAsync();
        var second = await CaptureTemplateAsync();

        first.Should().NotEqual(second);
        first.Length.Should().Be(SimulatedFingerCatalogue.TemplateLength);
    }

    [Fact]
    public async Task TheTemplateLeavingTheScannerIsAlreadyEncrypted()
    {
        // Rule 10.1: the adapter is the only place unencrypted sensor data exists.
        _host.Control.VeinScanner = VeinScannerMode.GoodRead;
        _scanner.SelectedFinger = SimulatedFingerCatalogue.All[0];

        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));

        var stored = SimulatedFingerCatalogue.PatternFor(_scanner.SelectedFinger);
        result.EncryptedTemplate!.Ciphertext.Should().NotEqual(stored);

        // Longer than the plaintext, because the nonce and tag travel with it.
        result.EncryptedTemplate.Ciphertext.Length
            .Should().BeGreaterThan(SimulatedFingerCatalogue.TemplateLength);
    }

    [Fact]
    public void TheCatalogueHasTwoFingersForEverySeededCustomer()
    {
        SimulatedFingerCatalogue.All.Should().HaveCount(20);
        SimulatedFingerCatalogue.All.Select(f => f.DisplayName).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void TheSamePatternComesBackEveryTimeButDiffersBetweenFingers()
    {
        var right = SimulatedFingerCatalogue.PatternFor("Thandiwe Mokoena", FingerPosition.RightIndex);
        var rightAgain = SimulatedFingerCatalogue.PatternFor("Thandiwe Mokoena", FingerPosition.RightIndex);
        var left = SimulatedFingerCatalogue.PatternFor("Thandiwe Mokoena", FingerPosition.LeftIndex);
        var someoneElse = SimulatedFingerCatalogue.PatternFor("Sipho Dlamini", FingerPosition.RightIndex);

        rightAgain.Should().Equal(right);
        left.Should().NotEqual(right, "the two enrolled fingers must be distinguishable");
        someoneElse.Should().NotEqual(right);
    }

    private async Task<byte[]> CaptureTemplateAsync()
    {
        var result = await _scanner.CaptureAsync(new VeinCaptureRequest(FingerPosition.RightIndex));
        return await _host.KeyVault.UnprotectAsync(result.EncryptedTemplate!);
    }
}
