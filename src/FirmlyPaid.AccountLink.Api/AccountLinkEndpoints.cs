using FirmlyPaid.Shared.Contracts;
using FirmlyPaid.Shared.Errors;
using FirmlyPaid.Shared.Security;

namespace FirmlyPaid.AccountLink.Api;

/// <summary>
/// Linking, listing and choosing bank accounts. Nothing here ever returns a balance or an
/// account number: a nickname, a bank and the last four digits are all a screen may show
/// (rules 10.7 and 10.8).
/// </summary>
public static class AccountLinkEndpoints
{
    public static IEndpointRouteBuilder MapAccountLinkEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var accounts = endpoints.MapGroup("/customers/{customerId:guid}/accounts").WithTags("Linked accounts");

        accounts.MapPost(string.Empty, async (
                Guid customerId,
                LinkAccountRequest request,
                AccountLinkService service,
                CancellationToken ct) =>
            {
                var response = await service.LinkAsync(customerId, request, ct);
                return Results.Created($"/customers/{customerId}/accounts/{response.LinkedAccountId}", response);
            })
            .WithName("LinkAccount")
            .WithSummary("Asks the bank to have the customer confirm an account in their own banking app.")
            .Produces<LinkAccountResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        accounts.MapGet(string.Empty, async (
                Guid customerId,
                AccountLinkService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListAsync(customerId, ct)))
            .WithName("ListAccounts")
            .WithSummary("Every account the customer holds, including any still waiting to be confirmed.")
            .Produces<IReadOnlyList<LinkedAccountSummary>>();

        accounts.MapGet("/payable", async (
                Guid customerId,
                AccountLinkService service,
                CancellationToken ct) =>
            Results.Ok(await service.ListPayableAsync(customerId, ct)))
            .WithName("ListPayableAccounts")
            .WithSummary("Only the confirmed accounts. This is what the bank picker at a till is built from.")
            .Produces<IReadOnlyList<LinkedAccountSummary>>();

        accounts.MapDelete("/{linkedAccountId:guid}", async (
                Guid customerId,
                Guid linkedAccountId,
                AccountLinkService service,
                CancellationToken ct) =>
            {
                await service.RemoveAsync(customerId, linkedAccountId, ct);
                return Results.NoContent();
            })
            .WithName("RemoveAccount")
            .WithSummary("Removes an account, clearing the default if that is what it was.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        endpoints.MapPut("/customers/{customerId:guid}/default-account", async (
                Guid customerId,
                SetDefaultAccountRequest request,
                AccountLinkService service,
                CancellationToken ct) =>
            {
                await service.SetDefaultAsync(customerId, request.LinkedAccountId, ct);
                return Results.NoContent();
            })
            .WithTags("Linked accounts")
            .WithName("SetDefaultAccount")
            .WithSummary("Chooses the account the bank picker skips straight to.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        endpoints.MapPost("/bank-callbacks/confirmations", async (
                BankConfirmationCallback callback,
                BankCallbackSignature signature,
                AccountLinkService service,
                CancellationToken ct) =>
            {
                // Checked before the message is used for anything. An unsigned callback
                // could attach someone else's bank account to a profile.
                if (!signature.IsValid(callback.ConfirmationReference, callback.Outcome, callback.Signature))
                {
                    throw new FirmlyPaidException(
                        ErrorCodes.Unauthorized,
                        "That confirmation was not signed by the bank.");
                }

                await service.ApplyConfirmationAsync(callback.ConfirmationReference, callback.Outcome, ct);

                // Always 200 once the signature holds, including for a reference we no
                // longer hold, so the bank stops retrying a callback we cannot use.
                return Results.Ok();
            })
            .WithTags("Bank callbacks")
            .WithName("BankConfirmationCallback")
            .WithSummary("The sponsor bank tells us the customer confirmed or rejected an account.")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }
}
