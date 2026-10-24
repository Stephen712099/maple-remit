using MapleRemit.Api.Contracts;

namespace MapleRemit.Api.Errors;

// Exceptions → JSON errors. Only unexpected ones are logged as errors.
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException
                                   && context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to send.
        }
        // Catches apply only before the response starts.
        catch (ApiException ex) when (!context.Response.HasStarted)
        {
            await WriteClientError(context, ex.StatusCode, new ErrorResponse(ex.Code, ex.Message));
        }
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            // Usually 400; 413/415 for big bodies or missing content type.
            await WriteClientError(context, ex.StatusCode, StatusCodeErrors.For(ex.StatusCode));
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception");
            await Write(context, StatusCodes.Status500InternalServerError,
                StatusCodeErrors.For(StatusCodes.Status500InternalServerError));
        }
    }

    private Task WriteClientError(HttpContext context, int status, ErrorResponse body)
    {
        StatusCodeErrors.LogClientError(logger, status, body.Code);
        return Write(context, status, body);
    }

    private static Task Write(HttpContext context, int status, ErrorResponse body)
    {
        // Drop headers set before the failure.
        context.Response.Clear();
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(body);
    }
}
