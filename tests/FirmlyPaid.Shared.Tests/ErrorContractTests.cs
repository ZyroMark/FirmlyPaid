using System.Text.Json;
using FirmlyPaid.Shared.Errors;
using FluentAssertions;
using Xunit;

namespace FirmlyPaid.Shared.Tests;

/// <summary>Part 6: every error response uses one shape, { code, message, traceId }.</summary>
public class ErrorContractTests
{
    [Fact]
    public void ApiError_SerialisesAsCodeMessageTraceId()
    {
        var json = JsonSerializer.Serialize(
            new ApiError(ErrorCodes.NoMatch, "We could not recognise that finger.", "trace-123"),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var properties = document.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        properties.Should().BeEquivalentTo("code", "message", "traceId");
        document.RootElement.GetProperty("code").GetString().Should().Be("NO_MATCH");
    }

    [Theory]
    [InlineData(ErrorCodes.NoMatch, 404)]
    [InlineData(ErrorCodes.TooManyAttempts, 429)]
    [InlineData(ErrorCodes.LivenessFailed, 422)]
    [InlineData(ErrorCodes.PinRequired, 428)]
    [InlineData(ErrorCodes.PinWrong, 401)]
    [InlineData(ErrorCodes.AccountNotConfirmed, 409)]
    [InlineData(ErrorCodes.BankDeclined, 402)]
    [InlineData(ErrorCodes.BankTimeout, 504)]
    [InlineData(ErrorCodes.LimitExceeded, 422)]
    [InlineData(ErrorCodes.CustomerFrozen, 403)]
    [InlineData(ErrorCodes.TerminalNotTrusted, 401)]
    public void EveryContractErrorCode_MapsToAStatus(string code, int expectedStatus)
    {
        ErrorStatusCodes.For(code).Should().Be(expectedStatus);
    }

    [Fact]
    public void UnknownCode_FallsBackToBadRequest()
    {
        ErrorStatusCodes.For("SOMETHING_NEW").Should().Be(400);
    }
}
