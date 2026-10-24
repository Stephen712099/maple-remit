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
            TransferService service) =>
        {
            var result = service.Create(request.CustomerId, request.QuoteId, idempotencyKey);
            var response = TransferResponse.From(result.Transfer);

            return result.Replayed
                ? TypedResults.Ok(response)
                : TypedResults.Created((string?)null, response);
        });

        return app;
    }
}
