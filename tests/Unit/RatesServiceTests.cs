using Application.Abstraction;
using Application.DTOs;
using Application.Options;
using Application.Services;
using Microsoft.Extensions.Options;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Xunit;
using System;

namespace Unit;

public class RatesServiceTests
{
    private readonly Mock<IExchangeRateProvider> _provider = new();
    private readonly Mock<ICacheService> _cache = new();
    private readonly RatesService _sut;

    public RatesServiceTests()
    {
        var caching = Options.Create(new CachingOptions { Enabled = false });
        _sut = new RatesService(_provider.Object, _cache.Object, caching);
    }


    [Fact]
    public async Task GetLatestAsync_ReturnsDto_AndCallsProvider()
    {
        // Arrange
        var rates = new Dictionary<string, decimal> { ["USD"] = 1.2m };
        _provider
            .Setup(p => p.GetLatestAsync("EUR", It.IsAny<CancellationToken>()))
            .ReturnsAsync(rates);

        // Act
        var dto = await _sut.GetLatestAsync("EUR", CancellationToken.None);

        // Assert
        Assert.Equal("EUR", dto.Base);
        Assert.True(dto.Rates.ContainsKey("USD"));
        Assert.Equal(System.DateOnly.FromDateTime(System.DateTime.UtcNow), dto.Date);

        _provider.Verify(p => p.GetLatestAsync("EUR", It.IsAny<CancellationToken>()), Times.Once);
        _provider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ConvertAsync_ComputesResult()
    {
        _provider.Setup(p => p.ConvertAsync(1m, "USD", "GBP", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0.8m);

        var dto = await _sut.ConvertAsync(10m, "USD", "GBP", default);

        Assert.Equal(10m * 0.8m, dto.Result);
    }

    // flip cache on/off
    private RatesService CreateSut(bool cacheEnabled, int historyTtlSeconds = 60)
    {
        var caching = Options.Create(new CachingOptions
        {
            Enabled = cacheEnabled,
            HistoryTtlSeconds = historyTtlSeconds
        });

        return new RatesService(_provider.Object, _cache.Object, caching);
    }

    [Fact]
    public async Task GetHistoricalAsync_ReturnsCached_WhenCacheHit()
    {
        // Arrange
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 3);
        var baseCurrency = "EUR";
        var page = 1;
        var pageSize = 10;

        var key = $"history:{baseCurrency}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:p{page}:s{pageSize}";

        var cached = new Paged<HistoricalRatesDto>(
            page, pageSize, 1,
             new List<HistoricalRatesDto>
            {
            new HistoricalRatesDto(new DateOnly(2024,1,1), new Dictionary<string, decimal>{{"USD", 1.1m}})
            });

        //  (cache HIT)
        Paged<HistoricalRatesDto>? outVal = cached;
        _cache.Setup(c => c.TryGet(key, out outVal!)).Returns(true);

        // enable cache for this test
        var sut = CreateSut(cacheEnabled: true);

        // Act
        var result = await sut.GetHistoricalAsync(start, end, baseCurrency, page, pageSize, CancellationToken.None);

        // Assert
        Assert.Same(cached, result);
        _provider.Verify(p => p.GetHistoricalAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _cache.Verify(c => c.Set(It.IsAny<string>(), It.IsAny<Paged<HistoricalRatesDto>>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task GetHistoricalAsync_CallsProvider_WhenCacheMiss_Paginates_AndCaches()
    {
        // Arrange
        var start = new DateOnly(2024, 1, 1);
        var end = new DateOnly(2024, 1, 5);
        var baseCurrency = "EUR";
        var page = 2;
        var pageSize = 2;

        var key = $"history:{baseCurrency}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:p{page}:s{pageSize}";

        // Cache MISS
        Paged<HistoricalRatesDto>? miss = null;
        _cache.Setup(c => c.TryGet(key, out miss!)).Returns(false);

        var map = new Dictionary<DateOnly, IReadOnlyDictionary<string, decimal>>
        {
            [new DateOnly(2024, 1, 3)] = new Dictionary<string, decimal> { ["USD"] = 1.3m },
            [new DateOnly(2024, 1, 1)] = new Dictionary<string, decimal> { ["USD"] = 1.1m },
            [new DateOnly(2024, 1, 5)] = new Dictionary<string, decimal> { ["USD"] = 1.5m },
            [new DateOnly(2024, 1, 2)] = new Dictionary<string, decimal> { ["USD"] = 1.2m },
            [new DateOnly(2024, 1, 4)] = new Dictionary<string, decimal> { ["USD"] = 1.4m },
        };

        _provider
            .Setup(p => p.GetHistoricalAsync(start, end, baseCurrency, It.IsAny<CancellationToken>()))
            .ReturnsAsync(map);

        var sut = CreateSut(cacheEnabled: true, historyTtlSeconds: 60);

        // Act
        var result = await sut.GetHistoricalAsync(start, end, baseCurrency, page, pageSize, CancellationToken.None);

        // Assert: pagination over 5 ordered items, page=2, size=2 => items [3rd,4th] = 2024-01-03 & 2024-01-04
        Assert.Equal(page, result.Page);
        Assert.Equal(pageSize, result.PageSize);
        Assert.Equal(5, result.Total);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(new DateOnly(2024, 1, 3), result.Items[0].Date);
        Assert.Equal(new DateOnly(2024, 1, 4), result.Items[1].Date);

        _provider.Verify(p => p.GetHistoricalAsync(start, end, baseCurrency, It.IsAny<CancellationToken>()), Times.Once);

        _cache.Verify(c => c.Set(
            It.Is<string>(k => k == key),
            It.Is<Paged<HistoricalRatesDto>>(p => p.Total == 5 && p.Items.Count == 2),
            It.Is<TimeSpan>(t => t == TimeSpan.FromSeconds(60))
        ), Times.Once);
    }

    [Fact]
    public async Task GetHistoricalAsync_Throws_WhenEndEarlierThanStart()
    {
        // Arrange
        var start = new DateOnly(2024, 1, 5);
        var end = new DateOnly(2024, 1, 1);
        var sut = CreateSut(cacheEnabled: false);

        // Act + Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.GetHistoricalAsync(start, end, "EUR", page: 1, pageSize: 10, CancellationToken.None));

        _provider.Verify(p => p.GetHistoricalAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
