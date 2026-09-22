using System.Reflection;
using System.Text.Json.Serialization;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace FirmlyPaid.Shared.Hosting;

/// <summary>
/// The wiring every FirmlyPaid service shares: validated config, the one error shape,
/// health endpoints and OpenAPI docs. Each service's Program.cs stays short enough to read.
/// </summary>
public static class FirmlyPaidServiceDefaults
{
    public static WebApplicationBuilder AddFirmlyPaidDefaults(
        this WebApplicationBuilder builder,
        string serviceName,
        string description)
    {
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "0.1.0";
        builder.Services.AddSingleton(new ServiceIdentity(serviceName, version));

        builder.Services.AddOptions<FirmlyPaidOptions>()
            .Bind(builder.Configuration.GetSection(FirmlyPaidOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<FirmlyPaidOptions>, FirmlyPaidOptionsValidator>();

        builder.Services.AddSingleton<IClock, SystemClock>();

        // Enums travel as their names so a till log reads "Approved", not "2".
        builder.Services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddHealthChecks();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = serviceName,
            Version = "v1",
            Description = description,
            Contact = new OpenApiContact { Name = "ZYROMARK PTY LTD" },
        }));

        return builder;
    }

    public static WebApplication UseFirmlyPaidDefaults(this WebApplication app)
    {
        // First in the pipeline: nothing may escape without the agreed error shape.
        app.UseMiddleware<ErrorHandlingMiddleware>();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapFirmlyPaidHealth();

        return app;
    }
}
