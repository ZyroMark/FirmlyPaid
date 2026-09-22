using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.TillIntegration.Api",
    description: "Till integration for large retailers. Never receives a customer's banks, ID digits or PIN.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see BUILD-SPEC.md part 11).

app.Run();
