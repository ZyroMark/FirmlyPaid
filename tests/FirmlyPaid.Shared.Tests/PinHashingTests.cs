using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>
/// Rule 10.6: PINs are hashed with a deliberately slow algorithm. A four digit PIN has
/// only ten thousand possibilities, so the cost of each guess is the whole defence.
/// </summary>
public class PinHashingTests
{
    private readonly Argon2idPinHasher _hasher = new();

    [Fact]
    public void TheRightPinVerifies()
    {
        var stored = _hasher.Hash("4821");

        _hasher.Verify("4821", stored).Should().BeTrue();
    }

    [Theory]
    [InlineData("4822")]
    [InlineData("1482")]
    [InlineData("48210")]
    [InlineData("482")]
    public void TheWrongPinDoesNot(string attempt)
    {
        var stored = _hasher.Hash("4821");

        _hasher.Verify(attempt, stored).Should().BeFalse();
    }

    [Fact]
    public void TheSamePinHashesDifferentlyEveryTime()
    {
        // A fresh salt per PIN, so a stolen table cannot be sorted into groups of
        // customers who happen to share a PIN.
        _hasher.Hash("4821").Should().NotBe(_hasher.Hash("4821"));
    }

    [Fact]
    public void TheStoredHashIsArgon2idAndCarriesItsCostSettings()
    {
        var stored = _hasher.Hash("4821");

        stored.Should().StartWith("$argon2id$v=19$m=19456,t=2,p=2$");

        // Written into the hash so the cost can be raised later without locking anyone out.
        stored.Split('$').Should().HaveCount(6);
    }

    [Fact]
    public void TheStoredHashDoesNotContainThePin()
    {
        _hasher.Hash("482196").Should().NotContain("482196");
    }

    [Fact]
    public void TheStoredHashFitsTheColumn()
    {
        // Customers.PinHash is nvarchar(512).
        _hasher.Hash("4821").Length.Should().BeLessThan(512);
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("12a4")]
    [InlineData("12 4")]
    public void APinOfTheWrongShapeIsRefused(string pin)
    {
        var act = () => _hasher.Hash(pin);

        act.Should().Throw<FirmlyPaidException>()
            .Which.Code.Should().Be(ErrorCodes.ValidationError);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("12345")]
    [InlineData("123456")]
    public void FourToSixDigitsIsAPin(string pin) => PinRules.IsValid(pin).Should().BeTrue();

    [Theory]
    [InlineData("not-a-hash")]
    [InlineData("$argon2id$v=19$m=19456,t=2,p=2$bad-base64$also-bad")]
    [InlineData("")]
    public void AHashWeCannotReadIsAHashThatCannotMatch(string stored)
    {
        _hasher.Verify("4821", stored).Should().BeFalse();
    }

    [Fact]
    public void ATamperedHashDoesNotVerify()
    {
        var stored = _hasher.Hash("4821");

        // Flip the last character of the hash portion.
        var tampered = stored[..^1] + (stored[^1] == 'A' ? 'B' : 'A');

        _hasher.Verify("4821", tampered).Should().BeFalse();
    }
}
