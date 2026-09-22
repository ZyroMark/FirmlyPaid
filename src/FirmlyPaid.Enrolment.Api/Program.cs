using FirmlyPaid.Data.Core;
using FirmlyPaid.Enrolment.Api;
using FirmlyPaid.Shared.Hosting;
using FirmlyPaid.Simulators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.Enrolment.Api",
    description: "Agent-assisted enrolment: Home Affairs verification, POPIA consent, finger vein capture and PIN.");

builder.Services.AddDbContext<FirmlyPaidCoreDbContext>(options =>
    options.UseSqlServer(builder.RequireConnectionString("Core")));

builder.Services.AddFirmlyPaidAdapters(builder.KeyFilePath());

builder.AddFirmlyPaidIdentityHashing();
builder.AddFirmlyPaidPinHashing();

// Everything biometric goes to the Matching service: this one never opens the vault.
builder.AddFirmlyPaidMatchingClient();

builder.Services.AddScoped<EnrolmentService>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FirmlyPaidCoreDbContext>("core-database");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

app.MapEnrolmentEndpoints();

app.Run();
