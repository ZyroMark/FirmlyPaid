using FirmlyPaid.Shared.Money;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// Amounts must read the same on a terminal in Khayelitsha and on a build server in
/// another timezone, so the format never follows the machine's culture.
/// </summary>
public class RandTests
{
    [Theory]
    [InlineData(250, "R250.00")]
    [InlineData(3000, "R3 000.00")]
    [InlineData(499.99, "R499.99")]
    [InlineData(0, "R0.00")]
    public void Format2_IsTheSameEverywhere(decimal amount, string expected)
    {
        Rand.Format2(amount).Should().Be(expected);
    }

    [Theory]
    [InlineData(250.00, 3.00)]       // 1.2% of R250.00
    [InlineData(199.99, 2.40)]       // 2.39988 rounds to 2.40
    [InlineData(20.625, 0.25)]       // half a cent rounds away from zero
    public void RoundToCents_RoundsHalfAwayFromZero(decimal amount, decimal expectedFee)
    {
        Rand.RoundToCents(amount * 1.20m / 100m).Should().Be(expectedFee);
    }
}
