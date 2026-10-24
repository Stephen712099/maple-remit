namespace MapleRemit.Api.Contracts;

// String, so bad values get INVALID_QUOTE_ID.
public sealed record CreateTransferRequest(string? QuoteId, string? CustomerId);
