using FirmlyPaid.Data.Core;
using FirmlyPaid.Data.Vault;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Clients;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators;
using FirmlyPaid.Simulators.Vein;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.MsSql;
using Xunit;

namespace FirmlyPaid.Enrolment.Api.Tests;

/// <summary>
/// Runs the Enrolment and Matching services together against a throwaway SQL Server, with
/// the enrolment kiosk's own simulated scanners. Nothing is stubbed out: an enrolment in
/// these tests writes real encrypted templates to a real vault through real HTTP.
/// </summary>
public sealed class EnrolmentTestHost : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    // Both services share one key file, standing in for the single KMS key they will
    // share in deployment. Without it, Matching could not read what Enrolment sends.
    private readonly string _keyFilePath =
        Path.Combine(Path.GetTempPath(), $"firmlypaid-enrolment-{Guid.NewGuid():N}.devkey");

    private WebApplicationFactory<Matching.Api.ApiMarker>? _matching;
    private WebApplicationFactory<ApiMarker>? _enrolment;

    private string _coreConnectionString = string.Empty;

    public HttpClient Client { get; private set; } = null!;

    /// <summary>Counts Home Affairs calls, so FR-03 can prove a bad ID never reached them.</summary>
    public HomeAffairsCallCounter HomeAffairsCalls { get; } = new();

    public SimulatorControlState Control => _enrolment!.Services.GetRequiredService<SimulatorControlState>();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        _coreConnectionString = ConnectionStringFor("FirmlyPaidCore");
        var vaultConnectionString = ConnectionStringFor("FirmlyPaidVault");

        await MigrateAsync(_coreConnectionString, vaultConnectionString);

        // Both services read their configuration while Program.cs runs, which is before a
        // test can reach the host builder. Environment variables are already in place.
        Environment.SetEnvironmentVariable("ConnectionStrings__Core", _coreConnectionString);
        Environment.SetEnvironmentVariable("ConnectionStrings__Vault", vaultConnectionString);
        Environment.SetEnvironmentVariable("FirmlyPaid__Security__KeyFilePath", _keyFilePath);
        Environment.SetEnvironmentVariable(
            "FIRMLYPAID_ID_PEPPER",
            "a-test-pepper-that-is-long-enough-to-pass");

        _matching = new WebApplicationFactory<Matching.Api.ApiMarker>();
        var matchingClient = _matching.CreateClient();

        _enrolment = new EnrolmentFactory(matchingClient, HomeAffairsCalls);
        Client = _enrolment.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _enrolment?.Dispose();
        _matching?.Dispose();
        await _container.DisposeAsync();

        if (File.Exists(_keyFilePath))
        {
            File.Delete(_keyFilePath);
        }
    }

    /// <summary>A core context on the same database, for checking what enrolment wrote.</summary>
    public FirmlyPaidCoreDbContext OpenCore() =>
        new(new DbContextOptionsBuilder<FirmlyPaidCoreDbContext>()
            .UseSqlServer(_coreConnectionString)
            .Options);

    /// <summary>The Matching service's own view, for checking the vault side of an enrolment.</summary>
    public IMatchingClient Matching => _enrolment!.Services.GetRequiredService<IMatchingClient>();

    /// <param name="QualityScore">What the scanner reported, passed on to the API unchanged.</param>
    public sealed record CapturedSample(EncryptedPayloadDto Payload, int QualityScore);

    /// <summary>Reads a test finger through the simulated scanner, as the kiosk would.</summary>
    public async Task<CapturedSample> ReadFingerAsync(string label, FingerPosition position)
    {
        var scanner = (SimulatedVeinScanner)_enrolment!.Services.GetRequiredService<IVeinScanner>();
        scanner.SelectedFinger = new SimulatedFingerCatalogue.SimulatedFinger(label, position);

        var capture = await scanner.CaptureAsync(new VeinCaptureRequest(position));

        return new CapturedSample(capture.EncryptedTemplate!.ToDto(), capture.QualityScore);
    }

    public async Task<IReadOnlyList<CapturedSample>> ReadFingerAsync(string label, FingerPosition position, int times)
    {
        var samples = new List<CapturedSample>();

        for (var i = 0; i < times; i++)
        {
            samples.Add(await ReadFingerAsync(label, position));
        }

        return samples;
    }

    /// <summary>The fingerprint the Home Affairs check is made against.</summary>
    public async Task<string> ReadFingerprintAsync()
    {
        var scanner = _enrolment!.Services.GetRequiredService<IFingerprintScanner>();
        var capture = await scanner.CaptureAsync();

        return Convert.ToBase64String(capture.Sample!);
    }

    /// <summary>Encrypts a PIN the way the terminal does before it leaves the device (rule 10.6).</summary>
    public async Task<EncryptedPayloadDto> EncryptPinAsync(string pin)
    {
        var keyVault = _enrolment!.Services.GetRequiredService<IKeyVault>();
        var protectedPin = await keyVault.ProtectAsync(System.Text.Encoding.UTF8.GetBytes(pin));

        return protectedPin.ToDto();
    }

    private string ConnectionStringFor(string databaseName) =>
        _container.GetConnectionString().Replace("Database=master", $"Database={databaseName}");

    private static async Task MigrateAsync(string coreConnectionString, string vaultConnectionString)
    {
        await using var core = new FirmlyPaidCoreDbContext(
            new DbContextOptionsBuilder<FirmlyPaidCoreDbContext>().UseSqlServer(coreConnectionString).Options);
        await core.Database.MigrateAsync();

        await using var vault = new FirmlyPaidVaultDbContext(
            new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>().UseSqlServer(vaultConnectionString).Options);
        await vault.Database.MigrateAsync();
    }

    /// <summary>
    /// The Enrolment service, with its Matching client pointed at the in-process Matching
    /// service and its Home Affairs adapter wrapped in a counter.
    /// </summary>
    private sealed class EnrolmentFactory(HttpClient matchingClient, HomeAffairsCallCounter counter)
        : WebApplicationFactory<ApiMarker>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.ConfigureTestServices(services =>
            {
                // Real HTTP, real JSON, real error codes: only the transport is in-process.
                services.RemoveAll<IMatchingClient>();
                services.AddSingleton<IMatchingClient>(new MatchingClient(matchingClient));

                var registered = services.Single(d => d.ServiceType == typeof(IHomeAffairsVerifier));
                services.Remove(registered);
                services.AddSingleton<IHomeAffairsVerifier>(provider => new CountingHomeAffairsVerifier(
                    (IHomeAffairsVerifier)registered.ImplementationFactory!(provider),
                    counter));
            });
    }
}

/// <summary>How many times Home Affairs has been asked, and about what.</summary>
public sealed class HomeAffairsCallCounter
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);

    public void Record() => Interlocked.Increment(ref _calls);

    public void Reset() => Interlocked.Exchange(ref _calls, 0);
}

/// <summary>Counts calls on the way through, and otherwise changes nothing.</summary>
public sealed class CountingHomeAffairsVerifier(IHomeAffairsVerifier inner, HomeAffairsCallCounter counter)
    : IHomeAffairsVerifier
{
    public Task<HomeAffairsVerificationResult> VerifyAsync(
        HomeAffairsVerificationRequest request,
        CancellationToken ct = default)
    {
        counter.Record();
        return inner.VerifyAsync(request, ct);
    }
}

[CollectionDefinition(nameof(EnrolmentCollection))]
public sealed class EnrolmentCollection : ICollectionFixture<EnrolmentTestHost>;
