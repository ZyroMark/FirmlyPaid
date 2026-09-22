using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FirmlyPaid.Data.Vault;

/// <summary>Design-time counterpart for the vault. See the note on the core factory.</summary>
public sealed class DesignTimeVaultDbContextFactory : IDesignTimeDbContextFactory<FirmlyPaidVaultDbContext>
{
    public FirmlyPaidVaultDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Vault")
            ?? """Server=(localdb)\FirmlyPaidDesignTime;Database=FirmlyPaidVault;Trusted_Connection=True;""";

        var options = new DbContextOptionsBuilder<FirmlyPaidVaultDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FirmlyPaidVaultDbContext(options);
    }
}
