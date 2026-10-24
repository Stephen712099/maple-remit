using MapleRemit.Api.Errors;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MapleRemit.Tests.Services;

public class QuoteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly InMemoryStore _store = new();
    private readonly QuoteService _service;

    public QuoteServiceTests()
    {
        var rate = Options.Create(new ExchangeRateOptions { ThbPerCad = 24.50m });
        _service = new QuoteService(_store, new FakeTimeProvider(Now), rate);
    }

    [Fact]
    public void Create_returns_priced_quote_that_expires_in_15_minutes()
    {
        var quote = _service.Create(231.00m);

        Assert.Equal(231.00m, quote.AmountCad);
        Assert.Equal(3.47m, quote.FeeCad);
        Assert.Equal(234.47m, quote.TotalCad);
        Assert.Equal(24.50m, quote.ExchangeRate);
        Assert.Equal(5659.50m, quote.AmountThb);
        Assert.Equal(Now, quote.CreatedAt);
        Assert.Equal(Now.AddMinutes(15), quote.ExpiresAt);
    }

    [Fact]
    public void Create_saves_the_quote()
    {
        var quote = _service.Create(100.00m);

        Assert.Equal(quote, _store.GetQuote(quote.Id));
    }

    [Fact]
    public void Create_rejects_invalid_amount()
    {
        var ex = Assert.Throws<ApiException>(() => _service.Create(9.99m));

        Assert.Equal(ErrorCodes.InvalidAmount, ex.Code);
    }
}
