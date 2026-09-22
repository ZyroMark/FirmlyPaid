using FirmlyPaid.Shared.Configuration;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// The pilot thresholds from parts 2 and 9 are defaults in code, so a service that is
/// handed no configuration still behaves the way the rules say.
/// </summary>
public class FirmlyPaidOptionsTests
{
    private readonly FirmlyPaidOptions _options = new();

    [Fact]
    public void PinIsRequiredFromFiveHundredRand()
    {
        _options.Payments.PinRequiredFromAmount.Should().Be(500.00m);
    }

    [Fact]
    public void PaymentLimitIsThreeThousandRand()
    {
        _options.Payments.MaxPaymentAmount.Should().Be(3000.00m);
    }

    [Fact]
    public void MerchantFeeIsOnePointTwoPercent()
    {
        _options.Fees.DefaultMerchantFeePercent.Should().Be(1.20m);
    }

    [Fact]
    public void EnrolmentCapturesTwoFingersWithThreeSamplesEach()
    {
        _options.Enrolment.RequiredFingers.Should().Be(2);
        _options.Enrolment.SamplesPerFinger.Should().Be(3);
    }

    [Fact]
    public void ACustomerMayLinkAtMostFiveAccounts()
    {
        _options.Enrolment.MaxLinkedAccounts.Should().Be(5);
    }

    [Fact]
    public void ThreeMatchAttemptsAndThreeWrongPinsAreTheCeiling()
    {
        _options.Matching.MaxMatchAttemptsPerPayment.Should().Be(3);
        _options.Payments.MaxWrongPinAttempts.Should().Be(3);
    }

    [Fact]
    public void FivePaymentsInTwoMinutesIsTheRiskWindow()
    {
        _options.Risk.MaxPaymentsPerWindow.Should().Be(5);
        _options.Risk.WindowMinutes.Should().Be(2);
    }

    [Fact]
    public void EveryOutsideDependencyDefaultsToASimulator()
    {
        _options.Adapters.VeinScanner.Should().Be(AdapterNames.Simulator);
        _options.Adapters.VeinMatcher.Should().Be(AdapterNames.Simulator);
        _options.Adapters.FingerprintScanner.Should().Be(AdapterNames.Simulator);
        _options.Adapters.HomeAffairsVerifier.Should().Be(AdapterNames.Simulator);
        _options.Adapters.BankGateway.Should().Be(AdapterNames.Simulator);
        _options.Adapters.SmsSender.Should().Be(AdapterNames.Simulator);
        _options.Adapters.KeyVault.Should().Be(AdapterNames.LocalDevFile);
    }
}
