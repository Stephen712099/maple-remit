namespace MapleRemit.Api.Contracts;

// String, so a malformed UUID string gets INVALID_QUOTE_ID. A non-string JSON value gets INVALID_REQUEST.
public sealed record CreateTransferRequest(string? QuoteId, string? CustomerId);
