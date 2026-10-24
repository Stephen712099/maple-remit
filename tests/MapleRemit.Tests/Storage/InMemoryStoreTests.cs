using MapleRemit.Api.Storage;

namespace MapleRemit.Tests.Storage;

public class InMemoryStoreTests : StoreContractTests
{
    private readonly InMemoryStore _store = new();

    protected override IQuoteStore Quotes => _store;
    protected override ITransferStore Transfers => _store;
}
