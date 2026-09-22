using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using FirmlyPaid.Simulators.Bank;
using FirmlyPaid.Simulators.Fingerprint;
using FirmlyPaid.Simulators.HomeAffairs;
using FirmlyPaid.Simulators.Keys;
using FirmlyPaid.Simulators.Sms;
using FirmlyPaid.Simulators.Vein;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FirmlyPaid.Simulators;

/// <summary>
/// The one place that turns the adapter names in configuration into real objects. This is
/// the swap point described in docs/architecture.md: a real device, bank or partner is a
/// new class registered under a new name, plus a value change in appsettings.json.
/// </summary>
public static class AdapterRegistration
{
    /// <summary>
    /// Registers an implementation for every outside dependency, chosen by
    /// FirmlyPaid:Adapters in configuration.
    /// </summary>
    /// <param name="keyFilePath">
    /// Where the development key file lives. Ignored once the KeyVault adapter is AwsKms.
    /// </param>
    public static IServiceCollection AddFirmlyPaidAdapters(
        this IServiceCollection services,
        string keyFilePath)
    {
        // Shared by the simulators, and the thing the admin console switches at demo time.
        services.AddSingleton<SimulatorControlState>();
        services.AddSingleton<FakeBankLedger>();
        services.AddSingleton<ISentSmsLog, InMemorySentSmsLog>();

        services.AddSingleton(provider => BuildKeyVault(provider, keyFilePath));
        services.AddSingleton(BuildVeinScanner);
        services.AddSingleton(BuildVeinMatcher);
        services.AddSingleton(BuildFingerprintScanner);
        services.AddSingleton(BuildHomeAffairsVerifier);
        services.AddSingleton(BuildBankGateway);
        services.AddSingleton(BuildSmsSender);

        return services;
    }

    private static IKeyVault BuildKeyVault(IServiceProvider provider, string keyFilePath) =>
        Adapters(provider).KeyVault switch
        {
            AdapterNames.LocalDevFile => new LocalDevFileKeyVault(
                keyFilePath,
                provider.GetRequiredService<ILogger<LocalDevFileKeyVault>>()),

            var name => throw NotBuiltYet("IKeyVault", name),
        };

    private static IVeinScanner BuildVeinScanner(IServiceProvider provider) =>
        Adapters(provider).VeinScanner switch
        {
            AdapterNames.Simulator => new SimulatedVeinScanner(
                provider.GetRequiredService<SimulatorControlState>(),
                provider.GetRequiredService<IKeyVault>(),
                provider.GetRequiredService<ILogger<SimulatedVeinScanner>>()),

            var name => throw NotBuiltYet("IVeinScanner", name),
        };

    private static IVeinMatcher BuildVeinMatcher(IServiceProvider provider) =>
        Adapters(provider).VeinMatcher switch
        {
            AdapterNames.Simulator => new SimulatedVeinMatcher(),
            var name => throw NotBuiltYet("IVeinMatcher", name),
        };

    private static IFingerprintScanner BuildFingerprintScanner(IServiceProvider provider) =>
        Adapters(provider).FingerprintScanner switch
        {
            AdapterNames.Simulator => new SimulatedFingerprintScanner(
                provider.GetRequiredService<SimulatorControlState>(),
                provider.GetRequiredService<ILogger<SimulatedFingerprintScanner>>()),

            var name => throw NotBuiltYet("IFingerprintScanner", name),
        };

    private static IHomeAffairsVerifier BuildHomeAffairsVerifier(IServiceProvider provider) =>
        Adapters(provider).HomeAffairsVerifier switch
        {
            AdapterNames.Simulator => new SimulatedHomeAffairsVerifier(
                provider.GetRequiredService<SimulatorControlState>(),
                provider.GetRequiredService<ILogger<SimulatedHomeAffairsVerifier>>()),

            var name => throw NotBuiltYet("IHomeAffairsVerifier", name),
        };

    private static IBankGateway BuildBankGateway(IServiceProvider provider) =>
        Adapters(provider).BankGateway switch
        {
            AdapterNames.Simulator => new SimulatedBankGateway(
                provider.GetRequiredService<FakeBankLedger>(),
                provider.GetRequiredService<SimulatorControlState>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<ILogger<SimulatedBankGateway>>()),

            var name => throw NotBuiltYet("IBankGateway", name),
        };

    private static ISmsSender BuildSmsSender(IServiceProvider provider) =>
        Adapters(provider).SmsSender switch
        {
            AdapterNames.Simulator => new SimulatedSmsSender(
                provider.GetRequiredService<ISentSmsLog>(),
                provider.GetRequiredService<IClock>(),
                provider.GetRequiredService<ILogger<SimulatedSmsSender>>()),

            var name => throw NotBuiltYet("ISmsSender", name),
        };

    private static AdapterOptions Adapters(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<FirmlyPaidOptions>>().Value.Adapters;

    /// <summary>
    /// A configured adapter with no implementation is a startup failure, not a silent
    /// fallback to the simulator. Falling back would mean a production deployment quietly
    /// paying with fake money.
    /// </summary>
    private static InvalidOperationException NotBuiltYet(string interfaceName, string adapterName) =>
        new($"No implementation of {interfaceName} is registered under the name '{adapterName}'. " +
            $"Check FirmlyPaid:Adapters in configuration, or add the adapter class and register it here.");
}
