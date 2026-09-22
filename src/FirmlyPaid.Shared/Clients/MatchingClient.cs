using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FirmlyPaid.Shared.Abstractions;
using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Errors;

namespace FirmlyPaid.Shared.Clients;

/// <summary>
/// Calls the Matching service over HTTP. An error from Matching keeps its code, so the
/// terminal sees NO_MATCH or QUALITY_TOO_LOW rather than a generic internal failure.
/// </summary>
public sealed class MatchingClient(HttpClient http) : IMatchingClient
{
    /// <summary>The name used with IHttpClientFactory, so the base address is configured once.</summary>
    public const string HttpClientName = "FirmlyPaid.Matching";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public Task<StoreTemplatesResponse> StoreTemplatesAsync(StoreTemplatesRequest request, CancellationToken ct = default) =>
        PostAsync<StoreTemplatesRequest, StoreTemplatesResponse>("/templates", request, ct);

    public Task<MatchResponse> MatchAsync(MatchRequest request, CancellationToken ct = default) =>
        PostAsync<MatchRequest, MatchResponse>("/match", request, ct);

    public async Task<TemplateSummaryResponse> GetSummaryAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var response = await http.GetAsync($"/templates/{templateOwnerId}", ct);
        return await ReadAsync<TemplateSummaryResponse>(response, ct);
    }

    public async Task<RevokeTemplatesResponse> RevokeAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var response = await http.PostAsync($"/templates/{templateOwnerId}/revoke", content: null, ct);
        return await ReadAsync<RevokeTemplatesResponse>(response, ct);
    }

    public async Task<DeleteTemplatesResponse> DeleteAsync(Guid templateOwnerId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"/templates/{templateOwnerId}", ct);
        return await ReadAsync<DeleteTemplatesResponse>(response, ct);
    }

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(path, body, JsonOptions, ct);
        return await ReadAsync<TResponse>(response, ct);
    }

    private static async Task<TResponse> ReadAsync<TResponse>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct)
                ?? throw new InvalidOperationException("The Matching service returned an empty body.");
        }

        // Matching speaks the same error shape as everyone else, so the code passes straight
        // through to the caller instead of being flattened into INTERNAL_ERROR.
        var error = await TryReadErrorAsync(response, ct);

        if (error is not null)
        {
            throw new FirmlyPaidException(error.Code, error.Message);
        }

        throw new InvalidOperationException(
            $"The Matching service answered {(int)response.StatusCode} {response.StatusCode}.");
    }

    private static async Task<ApiError?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.StatusCode == HttpStatusCode.NotFound &&
            response.Content.Headers.ContentType?.MediaType != "application/json")
        {
            return null;
        }

        try
        {
            return await response.Content.ReadFromJsonAsync<ApiError>(JsonOptions, ct);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
