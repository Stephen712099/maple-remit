namespace MapleRemit.Api.Errors;

public static class ErrorCodes
{
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidAmount = "INVALID_AMOUNT";
    public const string QuoteNotFound = "QUOTE_NOT_FOUND";
    public const string QuoteExpired = "QUOTE_EXPIRED";
    public const string QuoteAlreadyUsed = "QUOTE_ALREADY_USED";
    public const string DailyLimitExceeded = "DAILY_LIMIT_EXCEEDED";
    public const string IdempotencyKeyReused = "IDEMPOTENCY_KEY_REUSED";
    public const string NotFound = "NOT_FOUND";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";
    public const string PayloadTooLarge = "PAYLOAD_TOO_LARGE";
    public const string UnsupportedMediaType = "UNSUPPORTED_MEDIA_TYPE";
    public const string InternalError = "INTERNAL_ERROR";
}
