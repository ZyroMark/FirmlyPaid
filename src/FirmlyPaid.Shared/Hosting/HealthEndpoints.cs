using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FirmlyPaid.Shared.Hosting;

public static class HealthEndpoints
{
    /// <summary>
    /// Maps /health (is this process answering, with its dependency checks) and
    /// /health/live (is the process up at all, used by Docker Compose).
    /// </summary>
    public static IEndpointRouteBuilder MapFirmlyPaidHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = WriteHealthAsync })
            .WithTags("Health")
            .AllowAnonymous();

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteHealthAsync,
        })
            .WithTags("Health")
            .AllowAnonymous();

        return endpoints;
    }

    private static Task WriteHealthAsync(HttpContext context, HealthReport report)
    {
        var identity = context.RequestServices.GetRequiredService<ServiceIdentity>();

        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            service = identity.Name,
            version = identity.Version,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
