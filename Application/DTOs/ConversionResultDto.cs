namespace Application.DTOs;

public record ConversionResultDto(string From, string To, decimal Amount, decimal Rate, decimal Result, DateOnly Date);
