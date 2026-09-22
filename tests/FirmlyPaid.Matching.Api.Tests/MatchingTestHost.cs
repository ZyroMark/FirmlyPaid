using FirmlyPaid.Data.Vault;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Enums;
using FirmlyPaid.Shared.Security;
using FirmlyPaid.Simulators;
using FirmlyPaid.Simulators.Vein;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace FirmlyPaid.Matching.Api.Tests;

/// <summary>
/// Runs the real Matching service against a throwaway SQL Server, so these tests exercise
/// the same code path a terminal will: encrypted template in, coded answer out.
/// </summary>
public sealed class MatchingTestHost : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private readonly string _keyFilePath =
        Path.Combine(Path.GetTempPath(), $"firmlypaid-matching-{Guid.NewGuid():N}.devkey");

    private WebApplicationFactory<ApiMarker>? _factory;

    public HttpClient Client { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString().Replace("Database=master", "Database=FirmlyPaidVault");

        await using (var vault = new FirmlyPaidVaultDbContext(
            new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>().UseSqlServer(connectionString).Options))
        {
            await vault.Database.MigrateAsync();
        }

        // The service reads its configuration while Program.cs runs, which is before a
        // test can reach into the host builder. Environment variables are the one source
        // that is already in place by then.
        Environment.SetEnvironmentVariable("ConnectionStrings__Vault", connectionString);
        Environment.SetEnvironmentVariable("FirmlyPaid__Security__KeyFilePath", _keyFilePath);

        _factory = new WebApplicationFactory<ApiMarker>();
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        await _container.DisposeAsync();

        if (File.Exists(_keyFilePath))
        {
            File.Delete(_keyFilePath);
        }
    }

    /// <summary>The service's own key vault, so a test can encrypt a probe the way a scanner would.</summary>
    public IKeyVault KeyVault => Services.GetRequiredService<IKeyVault>();

    public SimulatorControlState Control => Services.GetRequiredService<SimulatorControlState>();

    private IServiceProvider Services => _factory!.Services;

    /// <summary>
    /// Reads a test finger through the simulated scanner, exactly as an enrolment kiosk
    /// would. Every read differs slightly, so nothing here is a byte comparison.
    /// </summary>
    public async Task<EncryptedPayloadDto> ReadFingerAsync(string label, FingerPosition position)
    {
        var scanner = (SimulatedVeinScanner)Services.GetRequiredService<IVeinScanner>();
        scanner.SelectedFinger = new SimulatedFingerCatalogue.SimulatedFinger(label, position);

        var capture = await scanner.CaptureAsync(new VeinCaptureRequest(position));

        return capture.EncryptedTemplate!.ToDto();
    }

    public async Task<IReadOnlyList<EncryptedPayloadDto>> ReadFingerAsync(string label, FingerPosition position, int times)
    {
        var samples = new List<EncryptedPayloadDto>();

        for (var i = 0; i < times; i++)
        {
            samples.Add(await ReadFingerAsync(label, position));
        }

        return samples;
    }

    /// <summary>A vault context pointed at the same database, for checking what was written.</summary>
    public FirmlyPaidVaultDbContext OpenVault() =>
        new(new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>()
            .UseSqlServer(_container.GetConnectionString().Replace("Database=master", "Database=FirmlyPaidVault"))
            .Options);
}

[CollectionDefinition(nameof(MatchingCollection))]
public sealed class MatchingCollection : ICollectionFixture<MatchingTestHost>;
