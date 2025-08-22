
namespace Application.Abstraction
{
    public interface IExchangeRateProvider
    {
        Task<IReadOnlyDictionary<string, decimal>> GetLatestAsync(string baseCurrency, CancellationToken ct);
        Task<decimal> ConvertAsync(decimal amount, string from, string to, CancellationToken ct);
        Task<IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> GetHistoricalAsync(DateOnly start, DateOnly end, string baseCurrency, CancellationToken ct);
    }
}
