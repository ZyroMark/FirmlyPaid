using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace FirmlyPaid.Risk.Api.Tests;

/// <summary>
/// Step 1 checkpoint: the service starts with its configuration validated and answers
/// /health with its own name.
/// </summary>
public class HealthEndpointTests : IClassFixture<WebApplicationFactory<FirmlyPaid.Risk.Api.ApiMarker>>
{
    private readonly WebApplicationFactory<FirmlyPaid.Risk.Api.ApiMarker> _factory;

    public HealthEndpointTests(WebApplicationFactory<FirmlyPaid.Risk.Api.ApiMarker> factory) => _factory = factory;

    [Fact]
    public async Task Health_ReportsHealthyAndNamesTheService()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("status").GetString().Should().Be("Healthy");
        document.RootElement.GetProperty("service").GetString().Should().Be("FirmlyPaid.Risk.Api");
    }

    [Fact]
    public async Task Liveness_AnswersWithoutRunningDependencyChecks()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task OpenApiDocument_IsPublished()
    {
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("FirmlyPaid.Risk.Api");
    }
}
