namespace MapleRemit.Api.Models;

public sealed record Transfer(
    Guid Id,
    string CustomerId,
    Guid QuoteId,
    decimal AmountCad,
    decimal FeeCad,
    decimal TotalCad,
    decimal ExchangeRate,
    decimal AmountThb,
    DateTimeOffset CreatedAt);
