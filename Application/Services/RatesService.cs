using Microsoft.Extensions.Options;
using Application.Contracts;
using Application.DTOs;
using Application.Options;
using Application.Abstraction;
using Shared;
namespace Application.Services;

public class RatesService : IRatesService
{
    private readonly IExchangeRateProvider _provider;
    private readonly ICacheService _cache;
    private readonly CachingOptions _caching;
    public RatesService(IExchangeRateProvider provider, ICacheService cache, IOptions<CachingOptions> caching)
    {
        _provider = provider;
        _cache = cache;
        _caching = caching.Value;
    }

    
    public async Task<LatestRatesDto> GetLatestAsync(string baseCurrency, CancellationToken ct)
    {
        Guards.ThrowIfExcluded(baseCurrency.ToUpperInvariant());

        var key = $"latest:{baseCurrency.ToUpperInvariant()}";
        if (_caching.Enabled && _cache.TryGet(key, out LatestRatesDto? cached) && cached is not null)
            return cached;

        var rates = await _provider.GetLatestAsync(baseCurrency, ct);
        var dto = new LatestRatesDto(baseCurrency.ToUpperInvariant(), DateOnly.FromDateTime(DateTime.UtcNow), rates);
        if (_caching.Enabled)
            _cache.Set(key, dto, TimeSpan.FromSeconds(_caching.LatestTtlSeconds));
        return dto;
    }
    public async Task<ConversionResultDto> ConvertAsync(decimal amount, string from, string to, CancellationToken ct)
    {
        Guards.ThrowIfExcluded(from.ToUpperInvariant());
        Guards.ThrowIfExcluded(to.ToUpperInvariant());
        var key = $"convert:{from.ToUpperInvariant()}:{to.ToUpperInvariant()}:{amount}";
        if (_caching.Enabled && _cache.TryGet(key, out ConversionResultDto? cached) && cached is not null)
            return cached;
        var rateResult = await _provider.ConvertAsync(1m, from, to, ct); 
        var converted = amount * rateResult;
        var dto = new ConversionResultDto(from.ToUpperInvariant(), to.ToUpperInvariant(), amount, rateResult, converted, DateOnly.FromDateTime(DateTime.UtcNow));
        if (_caching.Enabled) 
            _cache.Set(key, dto, TimeSpan.FromSeconds(_caching.ConvertTtlSeconds));

        return dto;
    }

    public async Task<Paged<HistoricalRatesDto>> GetHistoricalAsync(DateOnly start, DateOnly end, string baseCurrency, int page, int pageSize, CancellationToken ct)
    {
        Guards.ThrowIfExcluded(baseCurrency.ToUpperInvariant());
        if (end < start)
            throw new ArgumentException("end must be >= start");

        var key = $"history:{baseCurrency}:{start:yyyy-MM-dd}:{end:yyyy-MM-dd}:p{page}:s{pageSize}";
        if (_caching.Enabled && _cache.TryGet(key, out Paged<HistoricalRatesDto>? cached) && cached is not null)
            return cached;

        var map = await _provider.GetHistoricalAsync(start, end, baseCurrency, ct);
        var items = map.OrderBy(kv => kv.Key)
                       .Select(kv => new HistoricalRatesDto(kv.Key, kv.Value))
                       .ToList();
        var total = items.Count;
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        var result = new Paged<HistoricalRatesDto>(page, pageSize, total, pageItems);
        if (_caching.Enabled) 
            _cache.Set(key, result, TimeSpan.FromSeconds(_caching.HistoryTtlSeconds));

        return result;
    }

}
