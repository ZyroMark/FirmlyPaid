using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Risk.Api",
    description: "Internal risk scoring for a payment: allow, require PIN or block.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see BUILD-SPEC.md part 11).

app.Run();
