using MapleRemit.Api.Models;
using MapleRemit.Api.Services;

namespace MapleRemit.Api.Storage;

public sealed class InMemoryStore : IQuoteStore, ITransferStore
{
    // One lock guards all collections in this store.
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Quote> _quotes = new();
    private readonly HashSet<Guid> _usedQuoteIds = new();
    private readonly Dictionary<string, List<Transfer>> _transfersByCustomer = new();
    private readonly Dictionary<(string CustomerId, string Key), Transfer> _transfersByIdempotencyKey = new();

    public void AddQuote(Quote quote)
    {
        lock (_lock)
        {
            _quotes.Add(quote.Id, quote);
        }
    }

    public Quote? GetQuote(Guid id)
    {
        lock (_lock)
        {
            return _quotes.GetValueOrDefault(id);
        }
    }

    public TransferResult CreateTransfer(string customerId, Guid quoteId, string idempotencyKey, DateTimeOffset now)
    {
        lock (_lock)
        {
            // Key first: replays work after the quote expires.
            if (_transfersByIdempotencyKey.TryGetValue((customerId, idempotencyKey), out var existing))
            {
                if (existing.QuoteId != quoteId)
                    throw StoreErrors.IdempotencyKeyReused();

                return new TransferResult(existing, Replayed: true);
            }

            var quote = _quotes.GetValueOrDefault(quoteId)
                ?? throw StoreErrors.QuoteNotFound();

            var history = _transfersByCustomer.GetValueOrDefault(customerId) ?? [];
            var sentInWindowCad = history
                .Where(t => TransferRules.IsInDailyWindow(t.CreatedAt, now))
                .Sum(t => t.AmountCad);

            TransferRules.EnsureAllowed(quote, _usedQuoteIds.Contains(quoteId), sentInWindowCad, now);

            var transfer = new Transfer(Guid.NewGuid(), customerId, quote.Id, quote.AmountCad, quote.FeeCad,
                quote.TotalCad, quote.ExchangeRate, quote.AmountThb, now);

            if (!_transfersByCustomer.TryGetValue(customerId, out var transfers))
                _transfersByCustomer[customerId] = transfers = [];
            transfers.Add(transfer);
            _usedQuoteIds.Add(quoteId);
            _transfersByIdempotencyKey[(customerId, idempotencyKey)] = transfer;

            return new TransferResult(transfer, Replayed: false);
        }
    }
}
