using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FirmlyPaid.Data.Core;

/// <summary>
/// Lets "dotnet ef migrations add" build the model without starting a service.
/// Creating a migration does not touch a server, so the placeholder below is only ever a
/// shape for the provider. Real connection strings come from the environment (rule 10.15).
/// </summary>
public sealed class DesignTimeCoreDbContextFactory : IDesignTimeDbContextFactory<FirmlyPaidCoreDbContext>
{
    public FirmlyPaidCoreDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Core")
            ?? """Server=(localdb)\FirmlyPaidDesignTime;Database=FirmlyPaidCore;Trusted_Connection=True;""";

        var options = new DbContextOptionsBuilder<FirmlyPaidCoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new FirmlyPaidCoreDbContext(options);
    }
}
