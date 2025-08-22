namespace Application.DTOs;

public record HistoricalRatesDto(DateOnly Date, IReadOnlyDictionary<string, decimal> Rates);
