using MapleRemit.Api.Contracts;
using MapleRemit.Api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MapleRemit.Api.Endpoints;

public static class TransferEndpoints
{
    public static IEndpointRouteBuilder MapTransferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/transfers", Results<Created<TransferResponse>, Ok<TransferResponse>> (
            CreateTransferRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
            TransferService service,
            ILogger<TransferService> logger) =>
        {
            var result = service.Create(request.CustomerId, request.QuoteId, idempotencyKey);
            logger.LogInformation("Transfer {TransferId} for customer {CustomerId}, {AmountCad} CAD, replayed {Replayed}",
                result.Transfer.Id, result.Transfer.CustomerId, result.Transfer.AmountCad, result.Replayed);
            var response = TransferResponse.From(result.Transfer);

            return result.Replayed
                ? TypedResults.Ok(response)
                : TypedResults.Created((string?)null, response);
        });

        // String, so non-numeric pages get INVALID_PAGE.
        app.MapGet("/transfers", (string? customerId, string? page, TransferService service) =>
        {
            var (pageNumber, result) = service.List(customerId, page);
            var items = result.Items.Select(TransferResponse.From).ToList();
            return TypedResults.Ok(new TransferListResponse(items, pageNumber, TransferService.PageSize, result.TotalCount));
        });

        return app;
    }
}
