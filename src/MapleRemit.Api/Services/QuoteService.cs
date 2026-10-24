using System.Globalization;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Models;
using MapleRemit.Api.Storage;
using Microsoft.Extensions.Options;

namespace MapleRemit.Api.Services;

public sealed class QuoteService(IQuoteStore store, TimeProvider clock, IOptions<ExchangeRateOptions> rate)
{
    public static readonly TimeSpan QuoteLifetime = TimeSpan.FromMinutes(15);

    public Quote Create(decimal amountCad)
    {
        if (!Pricing.IsValidAmount(amountCad))
            throw new ApiException(ErrorCodes.InvalidAmount, string.Create(CultureInfo.InvariantCulture,
                $"amountCad must be between {Pricing.MinAmountCad} and {Pricing.MaxAmountCad} with at most 2 decimal places."));

        var thbPerCad = rate.Value.ThbPerCad;
        var fee = Pricing.Fee(amountCad);
        var now = clock.GetUtcNowMicroseconds();

        var quote = new Quote(
            Guid.NewGuid(),
            amountCad,
            fee,
            amountCad + fee,
            thbPerCad,
            Pricing.AmountThb(amountCad, thbPerCad),
            now,
            now + QuoteLifetime);

        store.AddQuote(quote);
        return quote;
    }
}
