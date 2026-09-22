using FirmlyPaid.Shared.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace FirmlyPaid.Shared.Hosting;

/// <summary>
/// The few settings a service needs before its options have been built, read in one place
/// so every Program.cs fails the same way when something is missing.
/// </summary>
public static class HostingConfiguration
{
    /// <summary>
    /// A connection string that the service cannot run without. Missing is a startup
    /// failure: a service that silently starts without its database is worse than one
    /// that refuses to start.
    /// </summary>
    public static string RequireConnectionString(this WebApplicationBuilder builder, string name)
    {
        var value = builder.Configuration.GetConnectionString(name);

        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException(
                $"The connection string '{name}' is not set. Set ConnectionStrings__{name} in the " +
                "environment, or use dotnet user-secrets locally (rule 10.15).")
            : value;
    }

    /// <summary>Where the development key file lives, before options binding has run.</summary>
    public static string KeyFilePath(this WebApplicationBuilder builder) =>
        builder.Configuration[$"{FirmlyPaidOptions.SectionName}:Security:KeyFilePath"] is { Length: > 0 } configured
            ? configured
            : new SecurityOptions().KeyFilePath;
}
