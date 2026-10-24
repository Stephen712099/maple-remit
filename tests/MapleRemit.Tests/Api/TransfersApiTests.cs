using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace MapleRemit.Tests.Api;

public class TransfersApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private const string Key1 = "11111111-1111-1111-1111-111111111111";
    private const string Key2 = "22222222-2222-2222-2222-222222222222";

    private readonly FakeTimeProvider _clock = new(Now);

    // Fresh app per client: empty store.
    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(_clock))).CreateClient();

    private static async Task<Guid> CreateQuote(HttpClient client, decimal amountCad)
    {
        var response = await client.PostAsJsonAsync("/quotes", new { amountCad });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("quoteId").GetGuid();
    }

    private static Task<HttpResponseMessage> PostTransfer(HttpClient client, Guid quoteId, string? key,
        string customerId = "cust-1")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = JsonContent.Create(new { quoteId, customerId }),
        };
        if (key is not null)
            request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(code, body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_transfer_returns_201_with_the_transfer()
    {
        var client = CreateClient();
        var quoteId = await CreateQuote(client, 231.00m);

        var response = await PostTransfer(client, quoteId, Key1);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("transferId").GetGuid());
        Assert.Equal(quoteId, body.GetProperty("quoteId").GetGuid());
        Assert.Equal("cust-1", body.GetProperty("customerId").GetString());
        Assert.Equal(231.00m, body.GetProperty("amountCad").GetDecimal());
        Assert.Equal(234.47m, body.GetProperty("totalCad").GetDecimal());
        Assert.Equal(5659.50m, body.GetProperty("amountThb").GetDecimal());
        Assert.Equal(Now, body.GetProperty("createdAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Repeated_request_with_same_key_returns_200_with_the_original_transfer()
    {
        var client = CreateClient();
        var quoteId = await CreateQuote(client, 100m);
        var first = await (await PostTransfer(client, quoteId, Key1)).Content.ReadFromJsonAsync<JsonElement>();

        var replay = await PostTransfer(client, quoteId, Key1);

        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        var body = await replay.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(first.GetProperty("transferId").GetGuid(), body.GetProperty("transferId").GetGuid());
    }

    [Fact]
    public async Task Missing_idempotency_key_returns_400()
    {
        var client = CreateClient();

        var response = await PostTransfer(client, await CreateQuote(client, 100m), key: null);

        await AssertError(response, HttpStatusCode.BadRequest, "MISSING_IDEMPOTENCY_KEY");
    }

    [Theory]
    [InlineData("""{ "customerId": "cust-1" }""")]
    [InlineData("""{ "customerId": "cust-1", "quoteId": "abc" }""")]
    public async Task Missing_or_malformed_quote_id_returns_400(string json)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("Idempotency-Key", Key1);

        var response = await CreateClient().SendAsync(request);

        await AssertError(response, HttpStatusCode.BadRequest, "INVALID_QUOTE_ID");
    }

    [Fact]
    public async Task Unknown_quote_returns_404()
    {
        var response = await PostTransfer(CreateClient(), Guid.NewGuid(), Key1);

        await AssertError(response, HttpStatusCode.NotFound, "QUOTE_NOT_FOUND");
    }

    [Fact]
    public async Task Used_quote_returns_409()
    {
        var client = CreateClient();
        var quoteId = await CreateQuote(client, 100m);
        await PostTransfer(client, quoteId, Key1);

        var response = await PostTransfer(client, quoteId, Key2);

        await AssertError(response, HttpStatusCode.Conflict, "QUOTE_ALREADY_USED");
    }

    [Fact]
    public async Task Expired_quote_returns_422()
    {
        var client = CreateClient();
        var quoteId = await CreateQuote(client, 100m);
        _clock.Advance(TimeSpan.FromMinutes(15));

        var response = await PostTransfer(client, quoteId, Key1);

        await AssertError(response, HttpStatusCode.UnprocessableEntity, "QUOTE_EXPIRED");
    }

    [Fact]
    public async Task Over_daily_limit_returns_422()
    {
        var client = CreateClient();
        await PostTransfer(client, await CreateQuote(client, 3000m), Key1);

        var response = await PostTransfer(client, await CreateQuote(client, 10m), Key2);

        await AssertError(response, HttpStatusCode.UnprocessableEntity, "DAILY_LIMIT_EXCEEDED");
    }

    [Fact]
    public async Task Same_key_with_a_different_quote_returns_422()
    {
        var client = CreateClient();
        await PostTransfer(client, await CreateQuote(client, 100m), Key1);

        var response = await PostTransfer(client, await CreateQuote(client, 200m), Key1);

        await AssertError(response, HttpStatusCode.UnprocessableEntity, "IDEMPOTENCY_KEY_REUSED");
    }
}
