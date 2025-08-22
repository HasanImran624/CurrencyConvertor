namespace Application.Options;

public class CachingOptions
{
    public bool Enabled { get; set; } = true;
    public int LatestTtlSeconds { get; set; } = 300;
    public int ConvertTtlSeconds { get; set; } = 60;
    public int HistoryTtlSeconds { get; set; } = 600;
}
