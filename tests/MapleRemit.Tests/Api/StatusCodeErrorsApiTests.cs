using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MapleRemit.Tests.Api;

// Framework errors also use the JSON format.
public class StatusCodeErrorsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task AssertJsonError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task Unknown_route_returns_404_not_found()
    {
        var response = await factory.CreateClient().GetAsync("/nope");

        await AssertJsonError(response, HttpStatusCode.NotFound, "NOT_FOUND");
    }

    [Fact]
    public async Task Wrong_method_returns_405_method_not_allowed()
    {
        var response = await factory.CreateClient().GetAsync("/quotes");

        await AssertJsonError(response, HttpStatusCode.MethodNotAllowed, "METHOD_NOT_ALLOWED");
    }

    [Fact]
    public async Task Wrong_content_type_returns_415_unsupported_media_type()
    {
        var response = await factory.CreateClient()
            .PostAsync("/quotes", new StringContent("x", Encoding.UTF8, "text/plain"));

        await AssertJsonError(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    [Fact]
    public async Task Missing_content_type_returns_415_unsupported_media_type()
    {
        var response = await factory.CreateClient()
            .PostAsync("/quotes", new ByteArrayContent("{}"u8.ToArray()));

        await AssertJsonError(response, HttpStatusCode.UnsupportedMediaType, "UNSUPPORTED_MEDIA_TYPE");
    }

    // Framework errors skip the middleware; they must still be counted.
    [Theory]
    [InlineData("/nope", "404 NOT_FOUND")]
    [InlineData("/quotes", "405 METHOD_NOT_ALLOWED")]
    public async Task Framework_errors_are_logged_as_client_errors(string path, string expected)
    {
        var logs = new CapturingLoggerProvider();
        var client = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();

        await client.GetAsync(path);

        var entry = Assert.Single(logs.Infos, e => e.Message.StartsWith("Client error"));
        Assert.Equal($"Client error {expected}", entry.Message);
        Assert.Empty(logs.Errors);
    }
}
