using MapleRemit.Api.Errors;
using MapleRemit.Api.Models;
using MapleRemit.Api.Services;
using MapleRemit.Api.Storage;

namespace MapleRemit.Tests.Storage;

// Tests every store must pass.
public abstract class StoreContractTests
{
    // Whole seconds: Postgres stores microseconds, so finer ticks would not round-trip.
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    protected abstract IQuoteStore Quotes { get; }
    protected abstract ITransferStore Transfers { get; }

    private Quote AddQuote(decimal amountCad, DateTimeOffset createdAt)
    {
        var fee = Pricing.Fee(amountCad);
        var quote = new Quote(Guid.NewGuid(), amountCad, fee, amountCad + fee, 24.50m,
            Pricing.AmountThb(amountCad, 24.50m), createdAt, createdAt + QuoteService.QuoteLifetime);
        Quotes.AddQuote(quote);
        return quote;
    }

    private static string NewKey() => Guid.NewGuid().ToString();

    private static void AssertRejected(string expectedCode, Action create)
    {
        var ex = Assert.Throws<ApiException>(create);
        Assert.Equal(expectedCode, ex.Code);
    }

    [Fact]
    public void Creates_transfer_from_quote()
    {
        var quote = AddQuote(231.00m, Now);

        var result = Transfers.CreateTransfer("cust-1", quote.Id, NewKey(), Now);

        Assert.False(result.Replayed);
        var transfer = result.Transfer;
        Assert.Equal("cust-1", transfer.CustomerId);
        Assert.Equal(quote.Id, transfer.QuoteId);
        Assert.Equal(231.00m, transfer.AmountCad);
        Assert.Equal(3.47m, transfer.FeeCad);
        Assert.Equal(234.47m, transfer.TotalCad);
        Assert.Equal(5659.50m, transfer.AmountThb);
        Assert.Equal(Now, transfer.CreatedAt);
    }

    [Fact]
    public void Unknown_quote_is_rejected()
    {
        var ex = Assert.Throws<ApiException>(() => Transfers.CreateTransfer("cust-1", Guid.NewGuid(), NewKey(), Now));

        Assert.Equal(ErrorCodes.QuoteNotFound, ex.Code);
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void Quote_cannot_be_used_twice()
    {
        var quote = AddQuote(100m, Now);
        Transfers.CreateTransfer("cust-1", quote.Id, NewKey(), Now);

        AssertRejected(ErrorCodes.QuoteAlreadyUsed, () => Transfers.CreateTransfer("cust-2", quote.Id, NewKey(), Now));
    }

    [Fact]
    public void Same_key_and_quote_returns_the_original_transfer()
    {
        var quote = AddQuote(100m, Now);
        var first = Transfers.CreateTransfer("cust-1", quote.Id, "key-1", Now);

        var replay = Transfers.CreateTransfer("cust-1", quote.Id, "key-1", Now.AddMinutes(1));

        Assert.True(replay.Replayed);
        Assert.Equal(first.Transfer, replay.Transfer);
    }

    [Fact]
    public void Replay_returns_the_original_transfer_after_the_quote_expires()
    {
        var quote = AddQuote(100m, Now);
        var first = Transfers.CreateTransfer("cust-1", quote.Id, "key-1", Now);

        var replay = Transfers.CreateTransfer("cust-1", quote.Id, "key-1", Now.AddHours(1));

        Assert.Equal(first.Transfer, replay.Transfer);
    }

    [Fact]
    public void Same_key_with_a_different_quote_is_rejected()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(100m, Now).Id, "key-1", Now);

        var ex = Assert.Throws<ApiException>(() => Transfers.CreateTransfer("cust-1", AddQuote(200m, Now).Id, "key-1", Now));

        Assert.Equal(ErrorCodes.IdempotencyKeyReused, ex.Code);
        Assert.Equal(422, ex.StatusCode);
    }

    [Fact]
    public void Idempotency_keys_are_scoped_per_customer()
    {
        var first = Transfers.CreateTransfer("cust-1", AddQuote(100m, Now).Id, "key-1", Now);

        var second = Transfers.CreateTransfer("cust-2", AddQuote(100m, Now).Id, "key-1", Now);

        Assert.False(second.Replayed);
        Assert.NotEqual(first.Transfer.Id, second.Transfer.Id);
    }

    [Fact]
    public void Failed_request_does_not_record_the_key()
    {
        var expired = AddQuote(100m, Now.AddHours(-1));
        AssertRejected(ErrorCodes.QuoteExpired, () => Transfers.CreateTransfer("cust-1", expired.Id, "key-1", Now));

        var result = Transfers.CreateTransfer("cust-1", AddQuote(100m, Now).Id, "key-1", Now);

        Assert.False(result.Replayed);
    }

    [Fact]
    public void Failed_request_does_not_use_the_quote()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(3000m, Now).Id, NewKey(), Now);
        var quote = AddQuote(100m, Now);
        AssertRejected(ErrorCodes.DailyLimitExceeded, () => Transfers.CreateTransfer("cust-1", quote.Id, NewKey(), Now));

        var result = Transfers.CreateTransfer("cust-2", quote.Id, NewKey(), Now);

        Assert.Equal(quote.Id, result.Transfer.QuoteId);
    }

    [Fact]
    public void Daily_limit_counts_transfers_in_the_last_24_hours()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(2000m, Now).Id, NewKey(), Now);
        Transfers.CreateTransfer("cust-1", AddQuote(1000m, Now).Id, NewKey(), Now); // total exactly 3000

        AssertRejected(ErrorCodes.DailyLimitExceeded,
            () => Transfers.CreateTransfer("cust-1", AddQuote(10m, Now).Id, NewKey(), Now));
    }

    [Fact]
    public void Transfers_drop_out_of_the_daily_limit_after_24_hours()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(3000m, Now).Id, NewKey(), Now);
        var later = Now.AddHours(24);

        var result = Transfers.CreateTransfer("cust-1", AddQuote(10m, later).Id, NewKey(), later);

        Assert.Equal(10m, result.Transfer.AmountCad);
    }

    [Fact]
    public void Daily_limit_is_per_customer()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(3000m, Now).Id, NewKey(), Now);

        var result = Transfers.CreateTransfer("cust-2", AddQuote(3000m, Now).Id, NewKey(), Now);

        Assert.Equal("cust-2", result.Transfer.CustomerId);
    }

    [Fact]
    public void Concurrent_requests_for_the_same_quote_create_one_transfer()
    {
        // Many rounds: the race window is short, so one round rarely hits it.
        for (var round = 0; round < ConcurrencyRounds; round++)
        {
            var quote = AddQuote(100m, Now);

            var outcomes = RunConcurrently(50, i => Transfers.CreateTransfer($"cust-{i}", quote.Id, NewKey(), Now));

            Assert.Single(outcomes, o => o.Result is not null);
            Assert.All(outcomes.Where(o => o.Error is not null),
                o => Assert.Equal(ErrorCodes.QuoteAlreadyUsed, Assert.IsType<ApiException>(o.Error).Code));
        }
    }

    [Fact]
    public void Concurrent_requests_cannot_exceed_the_daily_limit()
    {
        Transfers.CreateTransfer("cust-1", AddQuote(2000m, Now).Id, NewKey(), Now);
        var quotes = Enumerable.Range(0, 10).Select(_ => AddQuote(900m, Now)).ToArray();

        var outcomes = RunConcurrently(10, i => Transfers.CreateTransfer("cust-1", quotes[i].Id, NewKey(), Now));

        Assert.Single(outcomes, o => o.Result is not null);
        Assert.All(outcomes.Where(o => o.Error is not null),
            o => Assert.Equal(ErrorCodes.DailyLimitExceeded, Assert.IsType<ApiException>(o.Error).Code));
    }

    [Fact]
    public void Concurrent_requests_with_the_same_key_return_one_transfer()
    {
        for (var round = 0; round < ConcurrencyRounds; round++)
        {
            var quote = AddQuote(100m, Now);
            var key = NewKey();

            var outcomes = RunConcurrently(20, _ => Transfers.CreateTransfer("cust-1", quote.Id, key, Now));

            Assert.All(outcomes, o => Assert.Null(o.Error));
            Assert.Single(outcomes.Select(o => o.Result!.Transfer.Id).Distinct());
            Assert.Single(outcomes, o => !o.Result!.Replayed);
        }
    }

    private const int ConcurrencyRounds = 20;

    private sealed record Outcome(TransferResult? Result, Exception? Error);

    // Barrier: start all threads together.
    private static Outcome[] RunConcurrently(int count, Func<int, TransferResult> create)
    {
        var outcomes = new Outcome[count];
        using var start = new Barrier(count);
        var threads = Enumerable.Range(0, count).Select(i => new Thread(() =>
        {
            start.SignalAndWait();
            try { outcomes[i] = new Outcome(create(i), null); }
            catch (Exception ex) { outcomes[i] = new Outcome(null, ex); }
        })).ToList();

        threads.ForEach(t => t.Start());
        threads.ForEach(t => t.Join());
        return outcomes;
    }
}
