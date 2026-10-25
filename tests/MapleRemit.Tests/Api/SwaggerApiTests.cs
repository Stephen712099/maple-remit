using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MapleRemit.Tests.Api;

public class SwaggerApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Swagger_document_lists_the_endpoints()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"/quotes\"", body);
        Assert.Contains("\"/transfers\"", body);
        Assert.Contains("Idempotency-Key", body);
    }

    [Fact]
    public async Task Swagger_document_has_request_examples()
    {
        var body = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("\"amountCad\": 231", body);
        Assert.Contains("\"customerId\": \"cust-1\"", body);
    }

    [Fact]
    public async Task Swagger_ui_fills_a_blank_idempotency_key()
    {
        var script = await factory.CreateClient().GetStringAsync("/swagger/index.js");

        Assert.Contains("crypto.randomUUID()", script);
    }

    [Fact]
    public async Task Root_redirects_to_swagger()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.ToString());
    }
}
