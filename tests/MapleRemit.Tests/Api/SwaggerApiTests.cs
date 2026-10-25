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
    public async Task Root_redirects_to_swagger()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/swagger", response.Headers.Location?.ToString());
    }
}
