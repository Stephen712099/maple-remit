namespace MapleRemit.Api.Models;

public sealed record Quote(
    Guid Id,
    decimal AmountCad,
    decimal FeeCad,
    decimal TotalCad,
    decimal ExchangeRate,
    decimal AmountThb,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
