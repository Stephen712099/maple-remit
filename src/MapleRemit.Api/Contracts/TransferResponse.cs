using MapleRemit.Api.Models;

namespace MapleRemit.Api.Contracts;

public sealed record TransferResponse(
    Guid TransferId,
    string CustomerId,
    Guid QuoteId,
    decimal AmountCad,
    decimal FeeCad,
    decimal TotalCad,
    decimal ExchangeRate,
    decimal AmountThb,
    DateTimeOffset CreatedAt)
{
    public static TransferResponse From(Transfer transfer) => new(
        transfer.Id,
        transfer.CustomerId,
        transfer.QuoteId,
        transfer.AmountCad,
        transfer.FeeCad,
        transfer.TotalCad,
        transfer.ExchangeRate,
        transfer.AmountThb,
        transfer.CreatedAt);
}
