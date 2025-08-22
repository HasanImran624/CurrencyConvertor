using Application.DTOs;

namespace Application.Contracts;

public interface IRatesService
{
    Task<LatestRatesDto> GetLatestAsync(string baseCurrency, CancellationToken ct);
    
    
    Task<ConversionResultDto> ConvertAsync(decimal amount, string from, string to, CancellationToken ct);
    Task<Paged<HistoricalRatesDto>> GetHistoricalAsync(DateOnly start, DateOnly end, string baseCurrency, int page, int pageSize, CancellationToken ct);
}
