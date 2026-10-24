using MapleRemit.Api.Models;

namespace MapleRemit.Api.Storage;

public sealed class InMemoryStore : IQuoteStore
{
    // One lock guards all collections in this store.
    private readonly object _lock = new();
    private readonly Dictionary<Guid, Quote> _quotes = new();

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
}
