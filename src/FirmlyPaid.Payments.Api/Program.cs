using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Payments.Api",
    description: "Checkout: identify, choose bank, step up to PIN, and instruct the sponsor bank over PayShap.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see BUILD-SPEC.md part 11).

app.Run();
