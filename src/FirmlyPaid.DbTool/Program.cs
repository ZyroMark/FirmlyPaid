using FirmlyPaid.Data.Core;
using FirmlyPaid.Data.Core.Seeding;
using FirmlyPaid.Data.Vault;
using FirmlyPaid.DbTool;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Security;
using Microsoft.EntityFrameworkCore;

// One small tool for the two FirmlyPaid databases, so a developer never has to remember
// a dotnet ef incantation. Connection strings and the ID pepper come from the environment
// (rule 10.15); nothing secret is compiled in.

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";

try
{
    return command switch
    {
        "migrate" => await MigrateAsync(),
        "seed" => await SeedAsync(),
        "reset" => await ResetAsync(),
        "status" => await StatusAsync(),
        "verify-audit" => await VerifyAuditAsync(),
        _ => ShowHelp(),
    };
}
catch (EnvironmentSettingMissingException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 2;
}

async Task<int> MigrateAsync()
{
    await using var core = CreateCore();
    Console.WriteLine("Applying FirmlyPaidCore migrations ...");
    await core.Database.MigrateAsync();
    Console.WriteLine("  FirmlyPaidCore is up to date.");

    await using var vault = CreateVault();
    Console.WriteLine("Applying FirmlyPaidVault migrations ...");
    await vault.Database.MigrateAsync();
    Console.WriteLine("  FirmlyPaidVault is up to date.");

    return 0;
}

async Task<int> SeedAsync()
{
    await using var core = CreateCore();
    var seeder = new CoreSeeder(core, CreateIdentityHasher(), new SystemClock());

    var result = await seeder.SeedAsync();

    if (result.AlreadySeeded)
    {
        Console.WriteLine("FirmlyPaidCore already has the demo data. Nothing to do.");
        Console.WriteLine("Run 'reset' first if you want it rebuilt.");
        return 0;
    }

    Console.WriteLine("Seeded FirmlyPaidCore:");
    Console.WriteLine($"  merchants       {result.Merchants}");
    Console.WriteLine($"  stores          {result.Stores}");
    Console.WriteLine($"  terminals       {result.Terminals}");
    Console.WriteLine($"  customers       {result.Customers}");
    Console.WriteLine($"  linked accounts {result.LinkedAccounts}");
    Console.WriteLine();
    Console.WriteLine("Still to come: customer PINs are filled in at step 4, and encrypted bank");
    Console.WriteLine("account tokens plus vault templates at steps 3 to 5, once the simulators exist.");

    return 0;
}

async Task<int> ResetAsync()
{
    Console.WriteLine("This deletes every row in FirmlyPaidCore and FirmlyPaidVault on:");
    Console.WriteLine($"  {DescribeServer(RequireSetting("ConnectionStrings__Core"))}");
    Console.Write("Type RESET to continue: ");

    if (Console.ReadLine()?.Trim() != "RESET")
    {
        Console.WriteLine("Cancelled. Nothing was deleted.");
        return 1;
    }

    await using var core = CreateCore();
    await new CoreSeeder(core, CreateIdentityHasher(), new SystemClock()).ClearAsync();
    Console.WriteLine("  FirmlyPaidCore emptied.");

    await using var vault = CreateVault();
    await vault.VeinTemplates.ExecuteDeleteAsync();
    await vault.Buckets.ExecuteDeleteAsync();
    Console.WriteLine("  FirmlyPaidVault emptied.");

    return await SeedAsync();
}

async Task<int> StatusAsync()
{
    await using var core = CreateCore();
    Console.WriteLine("FirmlyPaidCore");
    Console.WriteLine($"  merchants       {await core.Merchants.CountAsync()}");
    Console.WriteLine($"  stores          {await core.Stores.CountAsync()}");
    Console.WriteLine($"  terminals       {await core.Terminals.CountAsync()}");
    Console.WriteLine($"  customers       {await core.Customers.CountAsync()}");
    Console.WriteLine($"  linked accounts {await core.LinkedAccounts.CountAsync()}");
    Console.WriteLine($"  consents        {await core.Consents.CountAsync()}");
    Console.WriteLine($"  enrolments      {await core.Enrolments.CountAsync()}");
    Console.WriteLine($"  payments        {await core.Payments.CountAsync()}");
    Console.WriteLine($"  audit rows      {await core.AuditLog.CountAsync()}");

    await using var vault = CreateVault();
    Console.WriteLine("FirmlyPaidVault");
    Console.WriteLine($"  vein templates  {await vault.VeinTemplates.CountAsync()}");
    Console.WriteLine($"  buckets         {await vault.Buckets.CountAsync()}");

    return 0;
}

async Task<int> VerifyAuditAsync()
{
    await using var core = CreateCore();
    var result = await new AuditChainVerifier(core).VerifyAsync();

    if (result.IsIntact)
    {
        Console.WriteLine($"Audit chain is intact across {result.RowsChecked} rows.");
        return 0;
    }

    Console.Error.WriteLine($"Audit chain is broken at sequence {result.BrokenAtSequence}.");
    Console.Error.WriteLine($"  {result.Reason}");
    return 3;
}

int ShowHelp()
{
    Console.WriteLine("""
        FirmlyPaid database tool

          dotnet run --project src\FirmlyPaid.DbTool -- <command>

        Commands
          migrate        Create or update both databases from the EF Core migrations.
          seed           Add the demo merchants, terminals and customers.
          reset          Delete everything and seed again. Asks for confirmation.
          status         Count the rows in each table.
          verify-audit   Recompute the audit log hash chain and report any tampering.

        Environment settings
          ConnectionStrings__Core    FirmlyPaidCore, required.
          ConnectionStrings__Vault   FirmlyPaidVault, required.
          FIRMLYPAID_ID_PEPPER       At least 32 characters. Used to hash ID numbers.

        On Windows, scripts\db-setup.ps1 reads these from your .env file for you.
        """);

    return 0;
}

FirmlyPaidCoreDbContext CreateCore()
{
    var options = new DbContextOptionsBuilder<FirmlyPaidCoreDbContext>()
        .UseSqlServer(RequireSetting("ConnectionStrings__Core"))
        .Options;

    return new FirmlyPaidCoreDbContext(options);
}

FirmlyPaidVaultDbContext CreateVault()
{
    var options = new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>()
        .UseSqlServer(RequireSetting("ConnectionStrings__Vault"))
        .Options;

    return new FirmlyPaidVaultDbContext(options);
}

IIdentityHasher CreateIdentityHasher() => new HmacIdentityHasher(RequireSetting("FIRMLYPAID_ID_PEPPER"));

string RequireSetting(string name)
{
    var value = Environment.GetEnvironmentVariable(name);

    return string.IsNullOrWhiteSpace(value)
        ? throw new EnvironmentSettingMissingException(name)
        : value;
}

/// <summary>Server and database only. A connection string must never be echoed in full.</summary>
string DescribeServer(string connectionString)
{
    var parts = connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(p => p.StartsWith("Server=", StringComparison.OrdinalIgnoreCase)
                 || p.StartsWith("Database=", StringComparison.OrdinalIgnoreCase));

    return string.Join(", ", parts);
}
