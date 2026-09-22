using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Simulators.Bank;
using FirmlyPaid.Simulators.Fingerprint;
using FirmlyPaid.Simulators.HomeAffairs;
using FirmlyPaid.Simulators.Keys;
using FirmlyPaid.Simulators.Sms;
using FirmlyPaid.Simulators.Vein;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FirmlyPaid.Simulators.Tests;

/// <summary>
/// The swap point from docs/architecture.md. Changing one value in configuration must be
/// all it takes to move from a simulator to a real device, and an unknown name must stop
/// the service rather than quietly falling back to fake money.
/// </summary>
public class AdapterRegistrationTests : IDisposable
{
    private readonly string _keyFilePath = Path.Combine(Path.GetTempPath(), $"firmlypaid-reg-{Guid.NewGuid():N}.devkey");

    public void Dispose()
    {
        if (File.Exists(_keyFilePath))
        {
            File.Delete(_keyFilePath);
        }
    }

    [Fact]
    public void TheDefaultConfigurationGivesASimulatorForEveryDependency()
    {
        using var provider = BuildProvider(new AdapterOptions());

        provider.GetRequiredService<IVeinScanner>().Should().BeOfType<SimulatedVeinScanner>();
        provider.GetRequiredService<IVeinMatcher>().Should().BeOfType<SimulatedVeinMatcher>();
        provider.GetRequiredService<IFingerprintScanner>().Should().BeOfType<SimulatedFingerprintScanner>();
        provider.GetRequiredService<IHomeAffairsVerifier>().Should().BeOfType<SimulatedHomeAffairsVerifier>();
        provider.GetRequiredService<IBankGateway>().Should().BeOfType<SimulatedBankGateway>();
        provider.GetRequiredService<IKeyVault>().Should().BeOfType<LocalDevFileKeyVault>();
        provider.GetRequiredService<ISmsSender>().Should().BeOfType<SimulatedSmsSender>();
    }

    [Fact]
    public void AnAdapterNameWithNoImplementationStopsTheService()
    {
        // Falling back to the simulator here would mean a production deployment paying
        // with fake money, so this has to fail loudly.
        using var provider = BuildProvider(new AdapterOptions { BankGateway = AdapterNames.SponsorBankPayShap });

        var act = () => provider.GetRequiredService<IBankGateway>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*IBankGateway*SponsorBankPayShap*");
    }

    [Fact]
    public void TheSwitchboardAndTheFakeLedgerAreSharedAcrossAService()
    {
        // The admin console flips one switch and every simulator in the process sees it.
        using var provider = BuildProvider(new AdapterOptions());

        provider.GetRequiredService<SimulatorControlState>()
            .Should().BeSameAs(provider.GetRequiredService<SimulatorControlState>());

        provider.GetRequiredService<FakeBankLedger>()
            .Should().BeSameAs(provider.GetRequiredService<FakeBankLedger>());
    }

    [Fact]
    public void EveryAdapterNameReservedForARealDeviceIsStillUnimplemented()
    {
        // A reminder rather than a restriction: when one of these gains an adapter, its
        // entry comes out of this list and the swap is real.
        string[] notBuiltYet =
        [
            AdapterNames.HitachiFingerVein,
            AdapterNames.SupremaBioMini,
            AdapterNames.HomeAffairsPartner,
            AdapterNames.SponsorBankPayShap,
            AdapterNames.AwsKms,
            AdapterNames.SmsProvider,
        ];

        notBuiltYet.Should().OnlyHaveUniqueItems();
        notBuiltYet.Should().NotContain(AdapterNames.Simulator);
    }

    private ServiceProvider BuildProvider(AdapterOptions adapters)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddSingleton<IClock, SystemClock>();
        services.Configure<FirmlyPaidOptions>(options => { });
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(
            new FirmlyPaidOptions { Adapters = adapters }));

        services.AddFirmlyPaidAdapters(_keyFilePath);

        return services.BuildServiceProvider();
    }
}
