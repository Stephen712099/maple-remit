using System.Globalization;
using MapleRemit.Api.Errors;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MapleRemit.Tests.Services;

public class TransferServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Now);
    private readonly QuoteService _quotes;
    private readonly TransferService _transfers;

    public TransferServiceTests()
    {
        var store = new InMemoryStore();
        _quotes = new QuoteService(store, _clock, Options.Create(new ExchangeRateOptions { ThbPerCad = 24.50m }));
        _transfers = new TransferService(store, _clock);
    }

    private string NewQuoteId() => _quotes.Create(100m).Id.ToString();

    private static string NewKey() => Guid.NewGuid().ToString();

    private static void AssertRejected(string expectedCode, Action create)
    {
        var ex = Assert.Throws<ApiException>(create);
        Assert.Equal(expectedCode, ex.Code);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Create_uses_the_current_time()
    {
        var quoteId = NewQuoteId();
        _clock.Advance(TimeSpan.FromMinutes(5));

        var result = _transfers.Create("cust-1", quoteId, NewKey());

        Assert.Equal(Now.AddMinutes(5), result.Transfer.CreatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_idempotency_key_is_rejected(string? key)
    {
        AssertRejected(ErrorCodes.MissingIdempotencyKey, () => _transfers.Create("cust-1", NewQuoteId(), key));
    }

    [Theory]
    [InlineData("key-1")]
    [InlineData("1")]
    public void Idempotency_key_that_is_not_a_uuid_is_rejected(string key)
    {
        AssertRejected(ErrorCodes.InvalidIdempotencyKey, () => _transfers.Create("cust-1", NewQuoteId(), key));
    }

    [Fact]
    public void Idempotency_key_is_case_insensitive()
    {
        var quoteId = NewQuoteId();
        var key = Guid.NewGuid().ToString();
        var first = _transfers.Create("cust-1", quoteId, key.ToLowerInvariant());

        var replay = _transfers.Create("cust-1", quoteId, key.ToUpperInvariant());

        Assert.True(replay.Replayed);
        Assert.Equal(first.Transfer.Id, replay.Transfer.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" cust-1")]
    [InlineData("cust 1")]
    [InlineData("cust@1")]
    public void Invalid_customer_id_is_rejected(string? customerId)
    {
        AssertRejected(ErrorCodes.InvalidCustomerId, () => _transfers.Create(customerId, NewQuoteId(), NewKey()));
    }

    [Fact]
    public void Customer_id_up_to_64_characters_is_allowed()
    {
        var result = _transfers.Create(new string('a', 64), NewQuoteId(), NewKey());

        Assert.Equal(64, result.Transfer.CustomerId.Length);
    }

    [Fact]
    public void Customer_id_over_64_characters_is_rejected()
    {
        AssertRejected(ErrorCodes.InvalidCustomerId, () => _transfers.Create(new string('a', 65), NewQuoteId(), NewKey()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Invalid_quote_id_is_rejected(string? quoteId)
    {
        AssertRejected(ErrorCodes.InvalidQuoteId, () => _transfers.Create("cust-1", quoteId, NewKey()));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("+2")]
    [InlineData(" 2")]
    public void Invalid_page_is_rejected(string page)
    {
        AssertRejected(ErrorCodes.InvalidPage, () => _transfers.List("cust-1", page));
    }

    [Fact]
    public void Missing_page_defaults_to_the_first_page()
    {
        _transfers.Create("cust-1", NewQuoteId(), NewKey());

        var (page, result) = _transfers.List("cust-1", null);

        Assert.Equal(1, page);
        Assert.Single(result.Items);
    }

    [Fact]
    public void Page_two_returns_the_next_20_transfers()
    {
        for (var i = 0; i < 25; i++)
            _transfers.Create("cust-1", _quotes.Create(10m).Id.ToString(), NewKey());

        var (page, result) = _transfers.List("cust-1", "2");

        Assert.Equal(2, page);
        Assert.Equal(5, result.Items.Count);
    }

    [Fact]
    public void Page_past_int_range_returns_an_empty_page()
    {
        _transfers.Create("cust-1", NewQuoteId(), NewKey());

        var (_, result) = _transfers.List("cust-1", int.MaxValue.ToString(CultureInfo.InvariantCulture));

        Assert.Empty(result.Items);
    }
}
