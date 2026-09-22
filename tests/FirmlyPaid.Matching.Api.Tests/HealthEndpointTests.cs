using System.Net;
using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Matching.Api.Tests;

/// <summary>
/// The service starts with its configuration validated, reaches its own database and
/// answers /health with its own name. From step 4 the vault connection is part of that
/// answer, so a service that cannot open the vault reports itself unhealthy.
/// </summary>
[Collection(nameof(MatchingCollection))]
public class HealthEndpointTests(MatchingTestHost host)
{
    [Fact]
    public async Task Health_ReportsHealthyAndNamesTheService()
    {
        var response = await host.Client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        document.RootElement.GetProperty("service").GetString().Should().Be("FirmlyPaid.Matching.Api");
    }

    [Fact]
    public async Task Health_IncludesTheVaultDatabaseCheck()
    {
        var response = await host.Client.GetAsync("/health");

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        document.RootElement.GetProperty("checks")
            .EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .Should().Contain("vault-database");
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
        (await response.Content.ReadAsStringAsync()).Should().Contain("FirmlyPaid.Matching.Api");
    }
}
