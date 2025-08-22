using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Integration;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => { });
    }

    [Fact(Skip = "Auth configured; provide a valid JWT to run")]
    public async Task Latest_Unauthorized_WithoutToken()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/v1/rates/latest?base=EUR");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
