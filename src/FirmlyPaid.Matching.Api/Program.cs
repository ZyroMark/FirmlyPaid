using FirmlyPaid.Data.Vault;
using FirmlyPaid.Matching.Api;
using FirmlyPaid.Shared.Hosting;
using FirmlyPaid.Simulators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Matching.Api",
    description: "Biometric vault and matching. Internal only: the one service allowed to read FirmlyPaidVault.");

// Rule 10.2: this is the only service given the vault connection string, and the only one
// that ever holds a decrypted template.
builder.Services.AddDbContext<FirmlyPaidVaultDbContext>(options =>
    options.UseSqlServer(builder.RequireConnectionString("Vault")));

builder.Services.AddFirmlyPaidAdapters(builder.KeyFilePath());

builder.Services.AddScoped<TemplateVault>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FirmlyPaidVaultDbContext>("vault-database");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

app.MapMatchingEndpoints();

app.Run();
