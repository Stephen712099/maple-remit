using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MapleRemit.Api.Models;
using MapleRemit.Api.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MapleRemit.Tests.Api;

public class ErrorLoggingApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly CapturingLoggerProvider _logs = new();

    private HttpClient CreateClient(IQuoteStore? store = null) =>
        factory.WithWebHostBuilder(b =>
        {
            b.ConfigureLogging(l => l.AddProvider(_logs));
            if (store is not null)
                b.ConfigureTestServices(s => s.AddSingleton<IQuoteStore>(store));
        }).CreateClient();

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Theory]
    [InlineData("""{ "amountCad": 9.99 }""")]
    [InlineData("{")]
    public async Task Client_errors_return_400_without_error_logs(string json)
    {
        var response = await CreateClient().PostAsync("/quotes", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(_logs.Errors);
    }

    [Fact]
    public async Task Unexpected_error_returns_500_and_is_logged_once_with_the_exception()
    {
        var response = await CreateClient(new FailingQuoteStore()).PostAsync("/quotes", Json("""{ "amountCad": 100.00 }"""));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.DoesNotContain("hunter2", body.GetRawText());

        var error = Assert.Single(_logs.Errors);
        var exception = Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal(FailingQuoteStore.Message, exception.Message);
    }

    private sealed class FailingQuoteStore : IQuoteStore
    {
        public const string Message = "db password is hunter2";

        public void AddQuote(Quote quote) => throw new InvalidOperationException(Message);
        public Quote? GetQuote(Guid id) => null;
    }
}
