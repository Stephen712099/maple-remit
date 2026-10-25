namespace MapleRemit.Api.Contracts;

public sealed record ErrorResponse(string Code, string Message)
{
    // Same value as TraceId in the logs, so support can find the request from a customer report.
    public string? TraceId { get; init; }
}
