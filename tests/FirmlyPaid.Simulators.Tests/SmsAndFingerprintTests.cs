using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators.Fingerprint;
using FirmlyPaid.Simulators.Sms;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>The two smaller simulators: the fingerprint reader and the SMS provider.</summary>
public class SmsAndFingerprintTests
{
    private readonly SimulatorControlState _control = new();
    private readonly TestClock _clock = new();

    [Fact]
    public async Task TheFingerprintReaderReturnsAStableSamplePerPerson()
    {
        var scanner = new SimulatedFingerprintScanner(_control, NullLogger<SimulatedFingerprintScanner>.Instance)
        {
            SelectedPersonLabel = "Thandiwe Mokoena",
        };

        var first = await scanner.CaptureAsync();
        var second = await scanner.CaptureAsync();

        first.IsUsable.Should().BeTrue();
        first.Sample.Should().Equal(second.Sample);
        first.QualityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task TwoPeopleGiveDifferentFingerprintSamples()
    {
        var scanner = new SimulatedFingerprintScanner(_control, NullLogger<SimulatedFingerprintScanner>.Instance);

        scanner.SelectedPersonLabel = "Thandiwe Mokoena";
        var thandiwe = await scanner.CaptureAsync();

        scanner.SelectedPersonLabel = "Sipho Dlamini";
        var sipho = await scanner.CaptureAsync();

        thandiwe.Sample.Should().NotEqual(sipho.Sample);
    }

    [Fact]
    public async Task NoFingerMeansNoSample()
    {
        var scanner = new SimulatedFingerprintScanner(_control, NullLogger<SimulatedFingerprintScanner>.Instance);
        _control.VeinScanner = VeinScannerMode.NoFinger;

        var result = await scanner.CaptureAsync();

        result.IsUsable.Should().BeFalse();
        result.Sample.Should().BeNull();
    }

    [Fact]
    public async Task ASentMessageIsReadableForTheDemoButTheNumberIsMasked()
    {
        var log = new InMemorySentSmsLog();
        var sender = new SimulatedSmsSender(log, _clock, NullLogger<SimulatedSmsSender>.Instance);

        await sender.SendAsync("0821000001", "Your FirmlyPaid code is 448120.");

        var sent = (await log.RecentAsync()).Should().ContainSingle().Subject;
        sent.Message.Should().Contain("448120", "a demo has to be able to read the code");
        sent.MaskedCellphoneNumber.Should().Be("082***0001");
        sent.MaskedCellphoneNumber.Should().NotBe("0821000001");
        sent.SentAtUtc.Should().Be(_clock.UtcNow);
    }

    [Fact]
    public async Task TheMostRecentMessagesComeBackFirst()
    {
        var log = new InMemorySentSmsLog();
        var sender = new SimulatedSmsSender(log, _clock, NullLogger<SimulatedSmsSender>.Instance);

        await sender.SendAsync("0821000001", "First");
        await sender.SendAsync("0821000002", "Second");
        await sender.SendAsync("0821000003", "Third");

        (await log.RecentAsync(2)).Select(m => m.Message).Should().Equal("Third", "Second");
    }

    [Fact]
    public async Task TheMessageLogDoesNotGrowWithoutBound()
    {
        var log = new InMemorySentSmsLog();
        var sender = new SimulatedSmsSender(log, _clock, NullLogger<SimulatedSmsSender>.Instance);

        for (var i = 0; i < 600; i++)
        {
            await sender.SendAsync("0821000001", $"Message {i}");
        }

        (await log.RecentAsync(1000)).Count.Should().BeLessThanOrEqualTo(500);
        (await log.RecentAsync(1)).Single().Message.Should().Be("Message 599");
    }

    [Fact]
    public void MaskingACellphoneLeavesNothingASearchCouldUse()
    {
        // The masked number is what reaches a log line and an admin page.
        var masked = Masking.MaskCellphone("0821000001");

        SensitiveDataPatterns.FindViolation(masked).Should().BeNull();
    }
}
