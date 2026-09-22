using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.AccountLink.Api",
    description: "Links customer bank accounts, confirmed by the customer in their own banking app.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see BUILD-SPEC.md part 11).

app.Run();
