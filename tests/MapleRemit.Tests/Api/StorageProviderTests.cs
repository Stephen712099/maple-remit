using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MapleRemit.Tests.Api;

public class StorageProviderTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void Startup_fails_when_postgres_has_no_connection_string()
    {
        var app = factory.WithWebHostBuilder(b => b.UseSetting("Storage:Provider", "Postgres"));

        var ex = Assert.Throws<InvalidOperationException>(() => app.CreateClient());
        Assert.Contains("ConnectionStrings:Postgres", ex.Message);
    }
}
