using MapleRemit.Api.Models;

namespace MapleRemit.Api.Storage;

public sealed record TransferResult(Transfer Transfer, bool Replayed);

public sealed record TransferPage(IReadOnlyList<Transfer> Items, int TotalCount);

public interface ITransferStore
{
    // Atomic: replay by key, or validate and create.
    TransferResult CreateTransfer(string customerId, Guid quoteId, string idempotencyKey, DateTimeOffset now);

    // Newest first.
    TransferPage ListTransfers(string customerId, int skip, int take);
}
