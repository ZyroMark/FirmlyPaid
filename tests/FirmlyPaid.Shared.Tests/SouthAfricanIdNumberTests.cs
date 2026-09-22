using FirmlyPaid.Shared.Security;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// The ID number rules that FR-03 and FR-06 lean on: a bad checksum must be caught
/// before we ever call Home Affairs, and digits 7 to 10 must resolve to a bucket.
/// </summary>
public class SouthAfricanIdNumberTests
{
    private const string ValidId = "8001015009087";

    [Fact]
    public void IsValid_AcceptsAWellFormedIdNumber()
    {
        SouthAfricanIdNumber.IsValid(ValidId).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("800101500908")]     // 12 digits
    [InlineData("80010150090870")]   // 14 digits
    [InlineData("80010150090A7")]    // not all digits
    [InlineData("8001015009088")]    // wrong check digit
    [InlineData("8013015009085")]    // month 13
    public void IsValid_RejectsBadInput(string? idNumber)
    {
        SouthAfricanIdNumber.IsValid(idNumber).Should().BeFalse();
    }

    [Fact]
    public void Bucket_ReturnsDigits7To10()
    {
        // 800101 5009 08 7 -> the SSSS group is 5009.
        SouthAfricanIdNumber.Bucket(ValidId).Should().Be(5009);
    }

    [Fact]
    public void Bucket_RefusesAnInvalidIdNumber()
    {
        var act = () => SouthAfricanIdNumber.Bucket("8001015009088");
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("5009", true, 5009)]
    [InlineData("0042", true, 42)]
    [InlineData("500", false, 0)]
    [InlineData("50a9", false, 0)]
    public void TryParseBucket_ValidatesTheFourDigitsTypedOnTheKeypad(string input, bool expected, int bucket)
    {
        SouthAfricanIdNumber.TryParseBucket(input, out var parsed).Should().Be(expected);
        if (expected)
        {
            parsed.Should().Be(bucket);
        }
    }

    [Fact]
    public void WithCheckDigit_ProducesAValidIdNumber()
    {
        var generated = SouthAfricanIdNumber.WithCheckDigit("900215548208");

        SouthAfricanIdNumber.IsValid(generated).Should().BeTrue();
        generated.Should().StartWith("900215548208");
    }
}
