using FirmlyPaid.AccountLink.Api;
using FirmlyPaid.Data.Core;
using FirmlyPaid.Shared.Hosting;
using FirmlyPaid.Simulators;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddFirmlyPaidDefaults(
    serviceName: "FirmlyPaid.AccountLink.Api",
    description: "Linking bank accounts to a customer, confirmed by the customer in their own banking app.");

builder.Services.AddDbContext<FirmlyPaidCoreDbContext>(options =>
    options.UseSqlServer(builder.RequireConnectionString("Core")));

builder.Services.AddFirmlyPaidAdapters(builder.KeyFilePath());

builder.AddFirmlyPaidBankCallbackSignature();

builder.Services.AddScoped<AccountLinkService>();

// The bank's own callback is the primary path; this catches the ones that go missing,
// and stands in for the callback entirely while the bank is simulated.
builder.Services.AddSingleton<BankConfirmationReconciler>();
builder.Services.AddHostedService<BankConfirmationPoller>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<FirmlyPaidCoreDbContext>("core-database");

var app = builder.Build();

app.UseFirmlyPaidDefaults();

app.MapAccountLinkEndpoints();

app.Run();
