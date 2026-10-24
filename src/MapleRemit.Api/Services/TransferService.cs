using MapleRemit.Api.Errors;
using MapleRemit.Api.Storage;

namespace MapleRemit.Api.Services;

public sealed class TransferService(ITransferStore store, TimeProvider clock)
{
    public const int PageSize = 20;
    public const int MaxCustomerIdLength = 64;

    public TransferResult Create(string? customerId, string? quoteId, string? idempotencyKey)
    {
        var key = ParseIdempotencyKey(idempotencyKey);
        EnsureCustomerId(customerId);
        if (!Guid.TryParse(quoteId, out var quoteGuid) || quoteGuid == Guid.Empty)
            throw new ApiException(ErrorCodes.InvalidQuoteId, "quoteId must be a non-empty UUID.");

        return store.CreateTransfer(customerId!, quoteGuid, key, clock.GetUtcNowMicroseconds());
    }

    public (int Page, TransferPage Result) List(string? customerId, string? page)
    {
        EnsureCustomerId(customerId);
        var pageNumber = 1;
        if (page is not null && (!int.TryParse(page, out pageNumber) || pageNumber < 1))
            throw new ApiException(ErrorCodes.InvalidPage, "page must be a whole number of 1 or greater.");

        // long math: no overflow on huge pages.
        var skip = (int)Math.Min((long)(pageNumber - 1) * PageSize, int.MaxValue);
        return (pageNumber, store.ListTransfers(customerId!, skip, PageSize));
    }

    // UUID only; canonical form makes it case-insensitive.
    private static string ParseIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ApiException(ErrorCodes.MissingIdempotencyKey, "The Idempotency-Key header is required.");

        if (!Guid.TryParse(idempotencyKey, out var key))
            throw new ApiException(ErrorCodes.InvalidIdempotencyKey, "The Idempotency-Key header must be a UUID.");

        return key.ToString("D");
    }

    private static void EnsureCustomerId(string? customerId)
    {
        var valid = !string.IsNullOrEmpty(customerId)
            && customerId.Length <= MaxCustomerIdLength
            && customerId.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');

        if (!valid)
            throw new ApiException(ErrorCodes.InvalidCustomerId,
                $"customerId must be 1 to {MaxCustomerIdLength} characters of letters, digits, '-' or '_'.");
    }
}
