using FirmlyPaid.Shared.Security;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// These patterns are the teeth behind rule 10.5. The step 11 log scan reuses them,
/// so they must catch the obvious leaks and stay quiet on safe text.
/// </summary>
public class SensitiveDataPatternsTests
{
    [Theory]
    [InlineData("Customer 8001015009087 enrolled")]
    [InlineData("account_number=1234567890")]
    [InlineData("pin: 4821")]
    [InlineData("Template = AQIDBAUGBwg=")]
    public void CatchesDataThatMustNeverBeLogged(string line)
    {
        SensitiveDataPatterns.FindViolation(line).Should().NotBeNull();
    }

    [Theory]
    [InlineData("Payment 3f2a approved for R250.00")]
    [InlineData("Request rejected with NO_MATCH")]
    [InlineData("Terminal 7 last seen at 2026-09-21T10:00:00Z")]
    public void StaysQuietOnSafeLogLines(string line)
    {
        SensitiveDataPatterns.FindViolation(line).Should().BeNull();
    }

    [Fact]
    public void MaskingHidesMostOfAnIdNumberAndCellphone()
    {
        Masking.MaskIdNumber("8001015009087").Should().Be("800101*******");
        Masking.MaskCellphone("0821234567").Should().Be("082***4567");
        Masking.Last4("1234567890").Should().Be("7890");
    }
}
