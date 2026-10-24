namespace MapleRemit.Api.Contracts;

public sealed record TransferListResponse(
    IReadOnlyList<TransferResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
