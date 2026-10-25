using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace MapleRemit.Tests.Api;

public class TransferLoggingApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Key = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task Created_and_replayed_transfers_are_logged_with_customer_and_amount()
    {
        var logs = new CapturingLoggerProvider();
        var client = factory.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();
        var quote = await (await client.PostAsJsonAsync("/quotes", new { amountCad = 231.00m }))
            .Content.ReadFromJsonAsync<JsonElement>();

        for (var i = 0; i < 2; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/transfers")
            {
                Content = JsonContent.Create(new { quoteId = quote.GetProperty("quoteId").GetGuid(), customerId = "cust-log" }),
            };
            request.Headers.Add("Idempotency-Key", Key);
            (await client.SendAsync(request)).EnsureSuccessStatusCode();
        }

        var entries = logs.Infos.Where(e => e.Message.StartsWith("Transfer ")).Select(e => e.Message).ToList();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, m => Assert.Contains("for customer cust-log, 231.00 CAD", m));
        Assert.EndsWith("replayed False", entries[0]);
        Assert.EndsWith("replayed True", entries[1]);
    }
}
