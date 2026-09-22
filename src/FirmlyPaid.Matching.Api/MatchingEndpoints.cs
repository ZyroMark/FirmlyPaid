using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Errors;

namespace FirmlyPaid.Matching.Api;

/// <summary>
/// The Matching service's API. Internal only: nothing here is published through the
/// Gateway, because a caller who can reach these endpoints can ask the vault questions
/// about anyone in a bucket (rule 10.2). Step 7 puts network rules behind that promise.
/// </summary>
public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var internalOnly = endpoints.MapGroup(string.Empty).WithTags("Matching (internal)");

        internalOnly.MapPost("/match", async (
                MatchRequest request,
                TemplateVault vault,
                CancellationToken ct) =>
            Results.Ok(await vault.MatchAsync(request, ct)))
            .WithName("Match")
            .WithSummary("Finds the customer a finger belongs to, within one ID digit bucket.")
            .Produces<MatchResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        internalOnly.MapPost("/templates", async (
                StoreTemplatesRequest request,
                TemplateVault vault,
                CancellationToken ct) =>
            Results.Ok(await vault.StoreAsync(request, ct)))
            .WithName("StoreTemplates")
            .WithSummary("Stores one finger's encrypted samples under the customer's transform.")
            .Produces<StoreTemplatesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        internalOnly.MapGet("/templates/{templateOwnerId:guid}", async (
                Guid templateOwnerId,
                TemplateVault vault,
                CancellationToken ct) =>
            Results.Ok(await vault.SummaryAsync(templateOwnerId, ct)))
            .WithName("GetTemplateSummary")
            .WithSummary("Counts what the vault holds for one owner. Returns no template bytes.")
            .Produces<TemplateSummaryResponse>();

        internalOnly.MapPost("/templates/{templateOwnerId:guid}/revoke", async (
                Guid templateOwnerId,
                TemplateVault vault,
                CancellationToken ct) =>
            Results.Ok(await vault.RevokeAsync(templateOwnerId, ct)))
            .WithName("RevokeTemplates")
            .WithSummary("Retires every live template for one owner so a leaked copy is worthless.")
            .Produces<RevokeTemplatesResponse>();

        internalOnly.MapDelete("/templates/{templateOwnerId:guid}", async (
                Guid templateOwnerId,
                TemplateVault vault,
                CancellationToken ct) =>
            Results.Ok(await vault.DeleteAsync(templateOwnerId, ct)))
            .WithName("DeleteTemplates")
            .WithSummary("Deletes every template for one owner. The customer must re-enrol.")
            .Produces<DeleteTemplatesResponse>();

        // A reminder in the published document, not a security control: the control is
        // that only this service holds the vault connection string.
        internalOnly.WithDescription(
            $"Internal service to service only. Unknown callers are refused with {ErrorCodes.Unauthorized} from step 7.");

        return endpoints;
    }
}
