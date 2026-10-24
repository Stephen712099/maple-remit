using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace MapleRemit.Tests.Api;

public class QuotesApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);

    private HttpClient CreateClient() =>
        factory.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now)))).CreateClient();

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Post_quote_returns_201_with_priced_quote()
    {
        var response = await CreateClient().PostAsync("/quotes", Json("""{ "amountCad": 231.00 }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(Guid.Empty, body.GetProperty("quoteId").GetGuid());
        Assert.Equal(231.00m, body.GetProperty("amountCad").GetDecimal());
        Assert.Equal(3.47m, body.GetProperty("feeCad").GetDecimal());
        Assert.Equal(234.47m, body.GetProperty("totalCad").GetDecimal());
        Assert.Equal(24.50m, body.GetProperty("exchangeRate").GetDecimal());
        Assert.Equal(5659.50m, body.GetProperty("amountThb").GetDecimal());
        Assert.Equal(Now.AddMinutes(15), body.GetProperty("expiresAt").GetDateTimeOffset());
    }

    [Theory]
    [InlineData("10.00")]
    [InlineData("3000.00")]
    public async Task Post_quote_at_amount_limits_returns_201(string amount)
    {
        var response = await CreateClient().PostAsync("/quotes", Json($$"""{ "amountCad": {{amount}} }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(amount, body.GetProperty("amountCad").GetRawText());
    }

    [Theory]
    [InlineData("9.99")]
    [InlineData("3000.01")]
    [InlineData("10.001")]
    public async Task Post_quote_with_invalid_amount_returns_400_invalid_amount(string amount)
    {
        var response = await CreateClient().PostAsync("/quotes", Json($$"""{ "amountCad": {{amount}} }"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_AMOUNT", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("{")]
    [InlineData("""{ "amountCad": "abc" }""")]
    [InlineData("""{ "amountCad": "100" }""")] // numbers sent as strings are rejected
    public async Task Post_quote_with_malformed_json_returns_400_invalid_request(string json)
    {
        var response = await CreateClient().PostAsync("/quotes", Json(json));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_REQUEST", body.GetProperty("code").GetString());
    }

    // ThrowOnBadRequest defaults to on only in Development.
    [Fact]
    public async Task Post_quote_with_malformed_json_returns_invalid_request_in_production()
    {
        var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

        var response = await client.PostAsync("/quotes", Json("{"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("INVALID_REQUEST", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("100")]
    [InlineData("100.0000")]
    public async Task Post_quote_returns_money_with_exactly_2_decimal_places(string amount)
    {
        var response = await CreateClient().PostAsync("/quotes", Json($$"""{ "amountCad": {{amount}} }"""));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("100.00", body.GetProperty("amountCad").GetRawText());
        Assert.Equal("2.99", body.GetProperty("feeCad").GetRawText());
        Assert.Equal("102.99", body.GetProperty("totalCad").GetRawText());
        Assert.Equal("2450.00", body.GetProperty("amountThb").GetRawText());
    }

    [Fact]
    public void Startup_fails_when_exchange_rate_is_not_positive()
    {
        var app = factory.WithWebHostBuilder(b => b.UseSetting("ExchangeRate:ThbPerCad", "0"));

        Assert.Throws<OptionsValidationException>(() => app.CreateClient());
    }
}
