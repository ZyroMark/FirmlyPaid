using FluentAssertions;
using Xunit;

namespace FirmlyPaid.EndToEnd.Tests;

/// <summary>
/// Placeholder so the project is wired into the test run from step 1.
/// The full enrol-then-pay journey and the Playwright flows land in steps 8 and 11.
/// </summary>
public class ScaffoldTests
{
    [Fact]
    public void EveryServiceHostIsReferenced()
    {
        string[] hosts =
        [
            typeof(FirmlyPaid.Gateway.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.Enrolment.Api.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.Matching.Api.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.AccountLink.Api.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.Payments.Api.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.Risk.Api.ApiMarker).Assembly.GetName().Name!,
            typeof(FirmlyPaid.TillIntegration.Api.ApiMarker).Assembly.GetName().Name!,
        ];

        hosts.Should().HaveCount(7).And.OnlyHaveUniqueItems();
    }
}
