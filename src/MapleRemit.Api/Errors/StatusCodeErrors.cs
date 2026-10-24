using MapleRemit.Api.Contracts;

namespace MapleRemit.Api.Errors;

public static class StatusCodeErrors
{
    // Single status → error-code mapping.
    public static ErrorResponse For(int status) => status switch
    {
        StatusCodes.Status400BadRequest => new(ErrorCodes.InvalidRequest, "The request is malformed."),
        >= StatusCodes.Status500InternalServerError => new(ErrorCodes.InternalError, "An unexpected error occurred."),
        _ => new(ErrorCodes.InvalidRequest, "The request is invalid."),
    };
}
