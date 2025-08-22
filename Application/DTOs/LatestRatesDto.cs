namespace Application.DTOs;

public record LatestRatesDto(string Base, DateOnly Date, IReadOnlyDictionary<string, decimal> Rates);
