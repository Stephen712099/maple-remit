using MapleRemit.Api.Contracts;
using MapleRemit.Api.Services;

namespace MapleRemit.Api.Endpoints;

public static class QuoteEndpoints
{
    public static IEndpointRouteBuilder MapQuoteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/quotes", (CreateQuoteRequest request, QuoteService service) =>
        {
            var quote = service.Create(request.AmountCad);
            return TypedResults.Created((string?)null, QuoteResponse.From(quote));
        });

        return app;
    }
}
