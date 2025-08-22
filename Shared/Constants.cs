namespace Shared
{
    public static class Constants
    {
        public static readonly HashSet<string> ExcludedCurrencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRY","PLN","THB","MXN"
    };
    }
}
