using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Enrolment.Api.Tests;

/// <summary>
/// The service starts with its configuration validated, reaches its own database and
/// answers /health with its own name. From step 4 the core database is part of that
/// answer, so a service that cannot open it reports itself unhealthy.
/// </summary>
[Collection(nameof(EnrolmentCollection))]
public class HealthEndpointTests(EnrolmentTestHost host)
{
    [Fact]
    public async Task Health_ReportsHealthyAndNamesTheService()
    {
        var response = await host.Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        document.RootElement.GetProperty("service").GetString().Should().Be("FirmlyPaid.Enrolment.Api");
    }

    [Fact]
    public async Task Health_IncludesTheCoreDatabaseCheck()
    {
        var response = await host.Client.GetAsync("/health");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should().Contain("core-database");
    }

    [Fact]
    public async Task Liveness_AnswersWithoutRunningDependencyChecks()
    {
        var response = await host.Client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApiDocument_IsPublished()
    {
        var response = await host.Client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var document = await response.Content.ReadAsStringAsync();
        document.Should().Contain("FirmlyPaid.Enrolment.Api");
        document.Should().Contain("/enrolments");
    }
}
