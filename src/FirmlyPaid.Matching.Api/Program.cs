using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Matching.Api",
    description: "Biometric vault and matching. Internal only: the one service allowed to read FirmlyPaidVault.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see CLAUDE.md part 11).

app.Run();
