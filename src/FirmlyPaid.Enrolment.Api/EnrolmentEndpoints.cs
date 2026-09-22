using FirmlyPaid.Shared.Contracts;

namespace FirmlyPaid.Enrolment.Api;

/// <summary>
/// The three calls an enrolment kiosk makes, in the order the agent works through them.
/// </summary>
public static class EnrolmentEndpoints
{
    public static IEndpointRouteBuilder MapEnrolmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var enrolments = endpoints.MapGroup("/enrolments").WithTags("Enrolment");

        enrolments.MapPost(string.Empty, async (
                CreateEnrolmentRequest request,
                EnrolmentService service,
                CancellationToken ct) =>
            {
                var response = await service.CreateAsync(request, ct);
                return Results.Created($"/enrolments/{response.EnrolmentId}", response);
            })
            .WithName("CreateEnrolment")
            .WithSummary("Verifies the customer with Home Affairs and records POPIA consent.")
            .Produces<CreateEnrolmentResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        enrolments.MapPost("/{enrolmentId:guid}/vein-samples", async (
                Guid enrolmentId,
                SubmitVeinSamplesRequest request,
                EnrolmentService service,
                CancellationToken ct) =>
            Results.Ok(await service.SubmitVeinSamplesAsync(enrolmentId, request, ct)))
            .WithName("SubmitVeinSamples")
            .WithSummary("Takes three encrypted samples of one finger. A poor read comes back with a retry message.")
            .Produces<SubmitVeinSamplesResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        enrolments.MapPost("/{enrolmentId:guid}/complete", async (
                Guid enrolmentId,
                CompleteEnrolmentRequest request,
                EnrolmentService service,
                CancellationToken ct) =>
            Results.Ok(await service.CompleteAsync(enrolmentId, request, ct)))
            .WithName("CompleteEnrolment")
            .WithSummary("Sets the customer's PIN and makes the profile active.")
            .Produces<CompleteEnrolmentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
