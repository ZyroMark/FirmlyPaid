using System.Net;
using System.Net.Http.Json;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Hosting;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// Nothing may leave a service except in the agreed error shape, and an unexpected fault
/// must never leak its message to a terminal (rule 10.5).
/// </summary>
public class ErrorHandlingMiddlewareTests
{
    private static async Task<IHost> StartHostAsync()
    {
        var host = await new HostBuilder()
            .ConfigureWebHost(web => web
                .UseTestServer()
                .ConfigureServices(services => services.AddSingleton(new ServiceIdentity("Test", "0.0.0")))
                .Configure(app =>
                {
                    app.UseMiddleware<ErrorHandlingMiddleware>();
                    app.Run(context =>
                    {
                        if (context.Request.Path == "/business")
                        {
                            throw FirmlyPaidException.LimitExceeded(3000m);
                        }

                        if (context.Request.Path == "/boom")
                        {
                            throw new InvalidOperationException("secret connection string details");
                        }

                        return context.Response.WriteAsync("ok");
                    });
                }))
            .StartAsync();

        return host;
    }

    [Fact]
    public async Task BusinessOutcome_KeepsItsCodeAndStatus()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/business");

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.Code.Should().Be(ErrorCodes.LimitExceeded);
        error.Message.Should().Contain("R3 000.00").And.NotBeEmpty();
        error.TraceId.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UnexpectedFault_BecomesInternalErrorAndHidesDetail()
    {
        using var host = await StartHostAsync();

        var response = await host.GetTestClient().GetAsync("/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("secret connection string details");

        var error = await response.Content.ReadFromJsonAsync<ApiError>();
        error!.Code.Should().Be(ErrorCodes.InternalError);
    }
}
