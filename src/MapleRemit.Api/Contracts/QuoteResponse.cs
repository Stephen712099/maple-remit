using MapleRemit.Api.Models;

namespace MapleRemit.Api.Contracts;

public sealed record QuoteResponse(
    Guid QuoteId,
    decimal AmountCad,
    decimal FeeCad,
    decimal TotalCad,
    decimal ExchangeRate,
    decimal AmountThb,
    DateTimeOffset ExpiresAt)
{
    public static QuoteResponse From(Quote quote) => new(
        quote.Id,
        quote.AmountCad,
        quote.FeeCad,
        quote.TotalCad,
        quote.ExchangeRate,
        quote.AmountThb,
        quote.ExpiresAt);
}
