using System.Net.Http.Json;
using Application.Abstraction;
using Application.Options;
using Microsoft.Extensions.Options;
namespace Infrastructure.Providers
{
    public class FrankfurterProvider : IExchangeRateProvider
    {
        private readonly HttpClient _http;
        public FrankfurterProvider(IHttpClientFactory factory, IOptions<FrankfurterOptions> options)
        {
            _http = factory.CreateClient("Frankfurter");
            _http.BaseAddress = new Uri(options.Value.BaseUrl);
        }
    

        public async Task<IReadOnlyDictionary<string, decimal>> GetLatestAsync(string baseCurrency, CancellationToken ct)
        {
            var url = $"latest?from={Uri.EscapeDataString(baseCurrency.ToUpperInvariant())}";
            var resp = await _http.GetFromJsonAsync<LatestResp>(url, ct) ?? throw new InvalidOperationException("Null response from provider");
            return resp.Rates ?? new Dictionary<string, decimal>();
        }


        public async Task<decimal> ConvertAsync(decimal amount, string from, string to, CancellationToken ct)
        {
            var url = $"latest?amount={amount}&from={Uri.EscapeDataString(from.ToUpperInvariant())}&to={Uri.EscapeDataString(to.ToUpperInvariant())}";
            var resp = await _http.GetFromJsonAsync<LatestResp>(url, ct) ?? throw new InvalidOperationException("Null response");
            if (resp.Rates is null || !resp.Rates.TryGetValue(to.ToUpperInvariant(), out var result))
                throw new InvalidOperationException("Conversion rate not found");
            // We return the unit rate when amount=1 to compute in service; but if amount != 1, compute a unit rate
            if (amount != 1m)
            {
                // Frankfurter returns the converted amount, so unit rate = converted/amount
                return result / amount;
            }
            return result;
        }

        public async Task<IReadOnlyDictionary<DateOnly, IReadOnlyDictionary<string, decimal>>> GetHistoricalAsync(DateOnly start, DateOnly end, string baseCurrency, CancellationToken ct)
        {
            var url = $"{start:yyyy-MM-dd}..{end:yyyy-MM-dd}?from={Uri.EscapeDataString(baseCurrency.ToUpperInvariant())}";
            var resp = await _http.GetFromJsonAsync<HistoryResp>(url, ct) ?? throw new InvalidOperationException("Null response");
            var dict = new Dictionary<DateOnly, IReadOnlyDictionary<string, decimal>>();
            if (resp.Rates is not null)
            {
                foreach (var kv in resp.Rates)
                {
                    if (DateOnly.TryParse(kv.Key, out var d))
                        dict[d] = kv.Value;
                }
            }
            return dict;
        }


        private sealed class LatestResp
        {
            public string? Base { get; set; } 
            public string? Date { get; set; }
            public Dictionary<string, decimal>? Rates { get; set; }
        }


        private sealed class HistoryResp
        {
            public string? Start_date { get; set; }
            public string? End_date { get; set; }
            public Dictionary<string, Dictionary<string, decimal>>? Rates { get; set; }
        }
    }
}
