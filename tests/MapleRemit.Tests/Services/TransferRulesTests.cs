using System.Globalization;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Models;
using MapleRemit.Api.Services;

namespace MapleRemit.Tests.Services;

public class TransferRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    // InlineData cannot hold decimals, so cases are strings.
    private static decimal D(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);

    private static Quote QuoteFor(decimal amountCad, DateTimeOffset expiresAt)
    {
        var fee = Pricing.Fee(amountCad);
        return new Quote(Guid.NewGuid(), amountCad, fee, amountCad + fee, 24.50m,
            Pricing.AmountThb(amountCad, 24.50m), expiresAt - QuoteService.QuoteLifetime, expiresAt);
    }

    private static void AssertRejected(string expectedCode, int expectedStatus, Action check)
    {
        var ex = Assert.Throws<ApiException>(check);
        Assert.Equal(expectedCode, ex.Code);
        Assert.Equal(expectedStatus, ex.StatusCode);
    }

    [Fact]
    public void Valid_quote_within_limit_is_allowed()
    {
        TransferRules.EnsureAllowed(QuoteFor(100m, Now.AddMinutes(1)), quoteUsed: false, sentInWindowCad: 0m, now: Now);
    }

    [Fact]
    public void Quote_one_tick_before_expiry_is_allowed()
    {
        TransferRules.EnsureAllowed(QuoteFor(100m, Now.AddTicks(1)), quoteUsed: false, sentInWindowCad: 0m, now: Now);
    }

    [Fact]
    public void Quote_at_exact_expiry_is_rejected()
    {
        AssertRejected(ErrorCodes.QuoteExpired, 422,
            () => TransferRules.EnsureAllowed(QuoteFor(100m, Now), quoteUsed: false, sentInWindowCad: 0m, now: Now));
    }

    [Fact]
    public void Used_quote_is_rejected()
    {
        AssertRejected(ErrorCodes.QuoteAlreadyUsed, 409,
            () => TransferRules.EnsureAllowed(QuoteFor(100m, Now.AddMinutes(1)), quoteUsed: true, sentInWindowCad: 0m, now: Now));
    }

    [Fact]
    public void Used_and_expired_quote_reports_already_used()
    {
        AssertRejected(ErrorCodes.QuoteAlreadyUsed, 409,
            () => TransferRules.EnsureAllowed(QuoteFor(100m, Now), quoteUsed: true, sentInWindowCad: 0m, now: Now));
    }

    [Theory]
    [InlineData("2000.00", "1000.00")] // total exactly 3000.00
    [InlineData("0.00", "3000.00")]
    public void Total_up_to_daily_limit_is_allowed(string sent, string amount)
    {
        TransferRules.EnsureAllowed(QuoteFor(D(amount), Now.AddMinutes(1)),
            quoteUsed: false, sentInWindowCad: D(sent), now: Now);
    }

    [Theory]
    [InlineData("2000.00", "1000.01")]
    [InlineData("2990.01", "10.00")]
    public void Total_over_daily_limit_is_rejected(string sent, string amount)
    {
        AssertRejected(ErrorCodes.DailyLimitExceeded, 422,
            () => TransferRules.EnsureAllowed(QuoteFor(D(amount), Now.AddMinutes(1)),
                quoteUsed: false, sentInWindowCad: D(sent), now: Now));
    }

    [Fact]
    public void Transfer_exactly_24_hours_old_is_outside_the_window()
    {
        Assert.False(TransferRules.IsInDailyWindow(Now.AddHours(-24), Now));
    }

    [Fact]
    public void Transfer_just_under_24_hours_old_is_inside_the_window()
    {
        Assert.True(TransferRules.IsInDailyWindow(Now.AddHours(-24).AddTicks(1), Now));
    }
}
