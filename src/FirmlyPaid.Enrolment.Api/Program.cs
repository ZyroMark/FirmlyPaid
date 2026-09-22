using FirmlyPaid.Shared.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Enrolment.Api",
    description: "Agent-assisted enrolment: Home Affairs verification, POPIA consent, finger vein capture and PIN.");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

// Endpoints for this service arrive in a later build step (see CLAUDE.md part 11).

app.Run();
