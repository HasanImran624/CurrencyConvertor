using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using Xunit;
using System.Net.Http.Json;
using System.Collections.Generic;
using System;


namespace Integration.Tests;

public class ApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient
        _client;

    public ApiTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;            
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Latest_Returns200_AndMapsRates()
    {
        // Arrange
        _factory.WireMock
           .Given(Request.Create().WithPath("/latest").UsingGet()
               .WithParam("from", "EUR"))
           .RespondWith(Response.Create()
               .WithStatusCode(200)
               .WithHeader("Content-Type", "application/json")
               .WithBody("""
                {
                  "amount": 1,
                  "base": "EUR",
                  "date": "2020-01-01",
                  "rates": { "USD": 1.12, "GBP": 0.85 }
                }
                """));
      
        var response = await _client.GetAsync("/api/v1/rates/latest?base=EUR");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<LatestDto>();
        Assert.NotNull(dto);
        Assert.Equal("EUR", dto!.Base);
        Assert.True(dto.Rates.ContainsKey("USD"));
    }

    [Fact]
    public async Task Convert_ExcludedCurrency_Returns400()
    {
        // TRY/PLN/THB/MXN excluded – no need to hit Frankfurter
        var resp = await _client.GetAsync("/api/v1/convert?from=USD&to=PLN&amount=10");
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Convert_Returns200_AndComputesResult()
    {
        // Arrange
        _factory.WireMock
            .Given(Request.Create().WithPath("/latest").UsingGet()
                .WithParam("amount", "1")
                .WithParam("from", "USD")
                .WithParam("to", "GBP"))
            .RespondWith(Response.Create()
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "amount": 1,
                  "base": "USD",
                  "date": "2020-01-01",
                  "rates": { "GBP": 0.8 }
                }
                """));

        var resp = await _client.GetAsync("/api/v1/convert?from=USD&to=GBP&amount=10");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<ConvertDto>();
        Assert.NotNull(dto);
        Assert.Equal(10m * 0.8m, dto!.Result);
        Assert.Equal(0.8m, dto.Rate);
    }

    [Fact]
    public async Task History_Returns200_Paginates()
    {
        // Arrange:
        _factory.WireMock
            .Given(Request.Create().WithPath("/2020-01-01..2020-01-03").UsingGet()
                .WithParam("from", "EUR"))
            .RespondWith(Response.Create()
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                {
                  "start_date": "2020-01-01",
                  "end_date": "2020-01-03",
                  "rates": {
                    "2020-01-01": { "USD": 1.10 },
                    "2020-01-02": { "USD": 1.11 },
                    "2020-01-03": { "USD": 1.12 }
                  }
                }
                """));

        // pageSize=2 => page1 has 2 items
        var resp = await _client.GetAsync("/api/v1/rates/history?start=2020-01-01&end=2020-01-03&base=EUR&page=1&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var dto = await resp.Content.ReadFromJsonAsync<PagedHistoryDto>();
        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Items.Count);
        Assert.Equal(3, dto.Total); // all items count
    }


    private sealed class LatestDto
    {
        public string Base { get; set; } = "";
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }
    private sealed class ConvertDto
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public decimal Amount { get; set; }
        public decimal Rate { get; set; }
        public decimal Result { get; set; }
    }
    private sealed class PagedHistoryDto
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
        public List<HistItem> Items { get; set; } = new();
    }
    private sealed class HistItem
    {
        public string Date { get; set; } = "";
        public Dictionary<string, decimal> Rates { get; set; } = new();
    }
}

  

