namespace Shared
{
    public static class Guards
    {
        public static void ThrowIfExcluded(string currencyCode)
        {
            if (Constants.ExcludedCurrencies.Contains(currencyCode))
                throw new ArgumentOutOfRangeException(nameof(currencyCode), $"Currency '{currencyCode}' is excluded.");
        }
    }
}
