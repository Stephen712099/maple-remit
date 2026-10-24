using MapleRemit.Api.Errors;

namespace MapleRemit.Api.Storage;

// Same errors for every store.
public static class StoreErrors
{
    public static ApiException QuoteNotFound() =>
        new(ErrorCodes.QuoteNotFound, "The quote was not found.", StatusCodes.Status404NotFound);

    public static ApiException IdempotencyKeyReused() =>
        new(ErrorCodes.IdempotencyKeyReused, "The Idempotency-Key was already used with a different request.",
            StatusCodes.Status422UnprocessableEntity);
}
