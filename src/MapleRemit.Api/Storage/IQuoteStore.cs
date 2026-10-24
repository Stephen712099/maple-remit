using MapleRemit.Api.Models;

namespace MapleRemit.Api.Storage;

public interface IQuoteStore
{
    void AddQuote(Quote quote);
    Quote? GetQuote(Guid id);
}
