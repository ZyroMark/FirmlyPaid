using FirmlyPaid.Data.Core;
using FirmlyPaid.Data.Vault;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using Xunit;

namespace FirmlyPaid.Data.Tests;

/// <summary>
/// A throwaway SQL Server in Docker, shared by every test in this project. Real SQL Server
/// rather than an in-memory provider, because most of what these tests check (unique
/// indexes, decimal precision, identity columns) only exists in the real database.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // The same image the compose stack uses, so nothing extra is pulled.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    private int _databaseCounter;

    public async Task InitializeAsync() => await _container.StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    /// <summary>
    /// A freshly migrated core database, one per test, so tests cannot affect each other.
    /// </summary>
    public async Task<FirmlyPaidCoreDbContext> CreateCoreAsync()
    {
        var name = $"Core_{Interlocked.Increment(ref _databaseCounter)}_{Guid.NewGuid():N}";
        var context = new FirmlyPaidCoreDbContext(
            new DbContextOptionsBuilder<FirmlyPaidCoreDbContext>()
                .UseSqlServer(ConnectionStringFor(name))
                .Options);

        await context.Database.MigrateAsync();
        return context;
    }

    public async Task<FirmlyPaidVaultDbContext> CreateVaultAsync()
    {
        var name = $"Vault_{Interlocked.Increment(ref _databaseCounter)}_{Guid.NewGuid():N}";
        var context = new FirmlyPaidVaultDbContext(
            new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>()
                .UseSqlServer(ConnectionStringFor(name))
                .Options);

        await context.Database.MigrateAsync();
        return context;
    }

    private string ConnectionStringFor(string databaseName) =>
        _container.GetConnectionString().Replace("Database=master", $"Database={databaseName}");
}

[CollectionDefinition(nameof(SqlServerCollection))]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>;
