using System.Globalization;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Models;

namespace MapleRemit.Api.Services;

// Pure rules, no I/O: safe inside a lock or transaction.
public static class TransferRules
{
    public const decimal DailyLimitCad = 3000.00m;
    public static readonly TimeSpan DailyWindow = TimeSpan.FromHours(24);

    public static bool IsInDailyWindow(DateTimeOffset createdAt, DateTimeOffset now) =>
        createdAt > now - DailyWindow;

    // "Used" before "expired": the more specific reason.
    public static void EnsureAllowed(Quote quote, bool quoteUsed, decimal sentInWindowCad, DateTimeOffset now)
    {
        if (quoteUsed)
            throw new ApiException(ErrorCodes.QuoteAlreadyUsed, "The quote has already been used.",
                StatusCodes.Status409Conflict);

        if (now >= quote.ExpiresAt)
            throw new ApiException(ErrorCodes.QuoteExpired, "The quote has expired.",
                StatusCodes.Status422UnprocessableEntity);

        if (sentInWindowCad + quote.AmountCad > DailyLimitCad)
            throw new ApiException(ErrorCodes.DailyLimitExceeded, string.Create(CultureInfo.InvariantCulture,
                    $"Transfers in the last 24 hours cannot exceed {DailyLimitCad} CAD."),
                StatusCodes.Status422UnprocessableEntity);
    }
}
