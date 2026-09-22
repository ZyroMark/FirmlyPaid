using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Gateway",
    description: "Single entry point for terminals, apps and portals. Handles authentication, terminal certificates and rate limits.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see CLAUDE.md part 11).

app.Run();
