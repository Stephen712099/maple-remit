using System.Text.Json;
using MapleRemit.Api.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace MapleRemit.Tests.Errors;

public class ErrorHandlingMiddlewareTests
{
    private readonly CapturingLoggerProvider _logs = new();

    private ErrorHandlingMiddleware CreateMiddleware(Exception exception) => CreateMiddleware(_ => throw exception);

    private ErrorHandlingMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, new Logger<ErrorHandlingMiddleware>(new LoggerFactory([_logs])));

    private async Task<(int Status, JsonElement Body)> Invoke(Exception exception)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        await CreateMiddleware(exception).InvokeAsync(context);

        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task ApiException_returns_400_with_its_code_and_message_logged_as_info()
    {
        var (status, body) = await Invoke(new ApiException(ErrorCodes.InvalidAmount, "Bad amount."));

        Assert.Equal(400, status);
        Assert.Equal("INVALID_AMOUNT", body.GetProperty("code").GetString());
        Assert.Equal("Bad amount.", body.GetProperty("message").GetString());
        Assert.Empty(_logs.Errors);
        AssertClientErrorLogged(400, "INVALID_AMOUNT");
    }

    [Fact]
    public async Task ApiException_returns_custom_status_code()
    {
        var (status, body) = await Invoke(
            new ApiException("QUOTE_ALREADY_USED", "Quote already used.", StatusCodes.Status409Conflict));

        Assert.Equal(409, status);
        Assert.Equal("QUOTE_ALREADY_USED", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task BadHttpRequestException_returns_400_invalid_request_logged_as_info()
    {
        var (status, body) = await Invoke(new BadHttpRequestException("Bad request."));

        Assert.Equal(400, status);
        Assert.Equal("INVALID_REQUEST", body.GetProperty("code").GetString());
        Assert.Empty(_logs.Errors);
        AssertClientErrorLogged(400, "INVALID_REQUEST");
    }

    // One info entry with status and code, no stack trace.
    private void AssertClientErrorLogged(int status, string code)
    {
        var entry = Assert.Single(_logs.Infos);
        Assert.Contains(status.ToString(), entry.Message);
        Assert.Contains(code, entry.Message);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task Unexpected_exception_returns_500_without_details_and_is_logged_once()
    {
        var thrown = new InvalidOperationException("db password is hunter2");

        var (status, body) = await Invoke(thrown);

        Assert.Equal(500, status);
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("An unexpected error occurred.", body.GetProperty("message").GetString());
        Assert.DoesNotContain("hunter2", body.GetRawText());
        Assert.Same(thrown, Assert.Single(_logs.Errors).Exception);
    }

    [Fact]
    public async Task Error_response_drops_headers_set_by_the_endpoint()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Response.Headers.Location = "/quotes/123";
            throw new InvalidOperationException("boom");
        });

        await middleware.InvokeAsync(context);

        Assert.Equal(500, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Location"));
    }

    [Fact]
    public async Task Client_disconnect_is_not_logged_as_error_and_writes_no_body()
    {
        using var aborted = new CancellationTokenSource();
        aborted.Cancel();
        var context = new DefaultHttpContext { RequestAborted = aborted.Token };
        context.Response.Body = new MemoryStream();

        await CreateMiddleware(new OperationCanceledException()).InvokeAsync(context);

        Assert.Empty(_logs.Errors);
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task Exception_after_response_started_is_rethrown()
    {
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        var thrown = new InvalidOperationException("boom");

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateMiddleware(thrown).InvokeAsync(context));

        Assert.Same(thrown, actual);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
